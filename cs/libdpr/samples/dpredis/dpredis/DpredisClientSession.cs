using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FASTER.core;
using FASTER.libdpr;

namespace dpredis
{
    internal class DpredisRequestFrame
    {
        public TaskCompletionSource<string> tcs;
        public long seqNum;

        public DpredisRequestFrame()
        {
            tcs = new TaskCompletionSource<string>();
            this.seqNum = 0;
        }

        public void Reset(long seqNum)
        {
            tcs = new TaskCompletionSource<string>();
            this.seqNum = seqNum;
        }
    }

    internal class DpredisBatch
    {
        private StringBuilder body = new StringBuilder();
        private int commandCount;
        private List<TaskCompletionSource<string>> tcs = new List<TaskCompletionSource<string>>();

        public void AddCommand(string command, TaskCompletionSource<string> tcs)
        {
            body.Append(command);
            commandCount++;
            this.tcs.Add(tcs);
        }

        public StringBuilder Body() => body;
        public int CommandCount() => commandCount;

        public List<TaskCompletionSource<string>> GetTcs() => tcs;

        public void Reset()
        {
            body.Clear();
            commandCount = 0;
            // Have to create new one. Old one is referred to by responses.
            tcs.Clear();
        }
    }

    internal class DpredisClientConnState : MessageUtil.AbstractConnState
    {
        private DprClientSession dprSession;
        private DpredisClientSession redisSession;
        private static readonly char[] redisSeparators = {'+', '\r', '\n'};

        public DpredisClientConnState(Socket socket, DprClientSession dprSession, DpredisClientSession redisSession)
        {
            Reset(socket);
            this.dprSession = dprSession;
            this.redisSession = redisSession;
        }

        protected override unsafe void HandleMessage(byte[] buf, int offset, int size)
        {
            fixed (byte* b = buf)
            {
                ref var header = ref Unsafe.AsRef<DprBatchResponseHeader>(b + offset);
                dprSession.ResolveBatch(ref header);
                // TODO(Tianyu): Eventually add more Redis types. Now we assume all responses are simple strings and therefore
                // use very simplistic parsing
                var redisResponse = Encoding.ASCII.GetString(buf, offset + header.Size(), size - header.Size());
                var entries = redisResponse.Split(redisSeparators, StringSplitOptions.RemoveEmptyEntries);
                var completedBatch = redisSession.GetOutstandingBatch(header.batchId);
                // Should be a one-to-one mapping between requests and reply
                Debug.Assert(entries.Length == completedBatch.GetTcs().Count);
                for (var i = 0; i < entries.Length; i++)
                    completedBatch.GetTcs()[i].SetResult(entries[i]);
                redisSession.ReturnResolvedBatch(completedBatch);
            }
        }
    }

    public class DpredisClientSession
    {
        private long seqNum;
        private int batchSize;

        private SimpleObjectPool<DpredisBatch> batchPool;
        private Dictionary<Worker, DpredisBatch> batches;
        private ConcurrentDictionary<int, DpredisBatch> outstandingBatches;
        private ConcurrentDictionary<Worker, (string, int)> routingTable;
        private Dictionary<Worker, Socket> conns;

        private DprClientSession dprSession;

        public DpredisClientSession(DprClient client,
            Guid id,
            ConcurrentDictionary<Worker, (string, int)> routingTable,
            int batchSize)
        {
            seqNum = 0;
            this.batchSize = batchSize;
            batchPool = new SimpleObjectPool<DpredisBatch>(() => new DpredisBatch());
            batches = new Dictionary<Worker, DpredisBatch>();
            outstandingBatches = new ConcurrentDictionary<int, DpredisBatch>();
            this.routingTable = routingTable;
            conns = new Dictionary<Worker, Socket>();
            dprSession = client.GetSession(id);
        }

        private Socket GetRedisConnection(Worker worker)
        {
            if (conns.TryGetValue(worker, out var result)) return result;
            var (ip, port) = routingTable[worker];
            var ipAddr = IPAddress.Parse(ip);
            result = new Socket(ipAddr.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            result.Connect(new IPEndPoint(ipAddr, port));
            result.NoDelay = true;
            conns.Add(worker, result);
            
            var saea = new SocketAsyncEventArgs();
            // TODO(Tianyu): Magic number buffer size
            saea.SetBuffer(new byte[1 << 20]);
            saea.Completed += MessageUtil.AbstractConnState.RecvEventArg_Completed;
            saea.UserToken = new DpredisClientConnState(result, dprSession, this);
            while (!result.ReceiveAsync(saea))
                MessageUtil.AbstractConnState.RecvEventArg_Completed(null, saea);
            return result;
        }

        private DpredisBatch GetCurrentBatch(Worker worker)
        {
            if (!batches.TryGetValue(worker, out var batch))
            {
                batch = batchPool.Checkout();
                batch.Reset();
                batches.Add(worker, batch);
            }
            return batch;
        }

        private void IssueBatch(Worker worker, DpredisBatch batch)
        {
            dprSession.IssueBatch(batch.CommandCount(), worker, out var dprBytes);
            var sock = GetRedisConnection(worker);
            sock.SendDpredisMessage(dprBytes, batch.Body().ToString());
            unsafe
            {
                fixed (byte* header = dprBytes)
                {
                    outstandingBatches.TryAdd(Unsafe.AsRef<DprBatchRequestHeader>(header).batchId, batch);
                }
            }
            batch = batchPool.Checkout();
            batch.Reset();
            batches.Add(worker, batch);
        }

        public Task<string> IssueCommand(Worker worker, string command, out long id)
        {
            var tcs = new TaskCompletionSource<string>();
            id = ++seqNum;
            var batch = GetCurrentBatch(worker);
            batch.AddCommand(command, tcs);
            if (batch.CommandCount() == batchSize)
                IssueBatch(worker, batch);
            return tcs.Task;
        }
        
        public void FlushAll()
        {
            foreach (var batch in batches)
            {
                if (batch.Value.CommandCount() != 0)
                    IssueBatch(batch.Key, batch.Value);
            }
        }

        internal DpredisBatch GetOutstandingBatch(int batchId)
        {
            outstandingBatches.Remove(batchId, out var result);
            return result;
        }

        internal void ReturnResolvedBatch(DpredisBatch batch) => batchPool.Return(batch);
    }
}