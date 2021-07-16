﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

using System;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using FASTER.common;
using FASTER.core;

namespace FASTER.server
{
    internal unsafe sealed class BinaryServerSession<Key, Value, Input, Output, Functions, ParameterSerializer>
        : FasterKVServerSessionBase<Key, Value, Input, Output, Functions, ParameterSerializer>
        where Functions : IFunctions<Key, Value, Input, Output, long>
        where ParameterSerializer : IServerSerializer<Key, Value, Input, Output>
    {
        readonly HeaderReaderWriter hrw;
        int readHead;

        int seqNo, pendingSeqNo, msgnum, start;
        byte* dcurr;

        readonly SubscribeKVBroker<Key, Value, IKeySerializer<Key>> subscribeKVBroker;

        public BinaryServerSession(Socket socket, FasterKV<Key, Value> store, Functions functions, ParameterSerializer serializer, MaxSizeSettings maxSizeSettings, SubscribeKVBroker<Key, Value, IKeySerializer<Key>> subscribeKVBroker)
            : base(socket, store, functions, serializer, maxSizeSettings)
        {
            this.subscribeKVBroker = subscribeKVBroker;

            readHead = 0;

            // Reserve minimum 4 bytes to send pending sequence number as output
            if (this.maxSizeSettings.MaxOutputSize < sizeof(int))
                this.maxSizeSettings.MaxOutputSize = sizeof(int);
        }

        public override int TryConsumeMessages(byte[] buf)
        {
            while (TryReadMessages(buf, out var offset))
                ProcessBatch(buf, offset);

            // The bytes left in the current buffer not consumed by previous operations
            var bytesLeft = bytesRead - readHead;
            if (bytesLeft != bytesRead)
            {
                // Shift them to the head of the array so we can reset the buffer to a consistent state
                Array.Copy(buf, readHead, buf, 0, bytesLeft);
                bytesRead = bytesLeft;
                readHead = 0;
            }

            return bytesRead;
        }

        public override void CompleteRead(ref Output output, long ctx, Status status)
        {
            byte* d = responseObject.obj.bufferPtr;
            var dend = d + responseObject.obj.buffer.Length;

            if ((int)(dend - dcurr) < 7 + maxSizeSettings.MaxOutputSize)
                SendAndReset(ref d, ref dend);

            hrw.Write(MessageType.PendingResult, ref dcurr, (int)(dend - dcurr));
            hrw.Write((MessageType)(ctx >> 32), ref dcurr, (int)(dend - dcurr));
            Write((int)(ctx & 0xffffffff), ref dcurr, (int)(dend - dcurr));
            Write(ref status, ref dcurr, (int)(dend - dcurr));
            if (status != Status.NOTFOUND)
                serializer.Write(ref output, ref dcurr, (int)(dend - dcurr));
            msgnum++;
        }

        public override void CompleteRMW(ref Output output, long ctx, Status status)
        {
            byte* d = responseObject.obj.bufferPtr;
            var dend = d + responseObject.obj.buffer.Length;

            if ((int)(dend - dcurr) < 7 + maxSizeSettings.MaxOutputSize)
                SendAndReset(ref d, ref dend);

            hrw.Write(MessageType.PendingResult, ref dcurr, (int)(dend - dcurr));
            hrw.Write((MessageType)(ctx >> 32), ref dcurr, (int)(dend - dcurr));
            Write((int)(ctx & 0xffffffff), ref dcurr, (int)(dend - dcurr));
            Write(ref status, ref dcurr, (int)(dend - dcurr));
            if (status == Status.OK || status == Status.NOTFOUND)
                serializer.Write(ref output, ref dcurr, (int)(dend - dcurr));
            msgnum++;
        }

        private bool TryReadMessages(byte[] buf, out int offset)
        {
            offset = default;

            var bytesAvailable = bytesRead - readHead;
            // Need to at least have read off of size field on the message
            if (bytesAvailable < sizeof(int)) return false;

            // MSB is 1 to indicate binary protocol
            var size = -BitConverter.ToInt32(buf, readHead);
            // Not all of the message has arrived
            if (bytesAvailable < size + sizeof(int)) return false;
            offset = readHead + sizeof(int);

            // Consume this message and the header
            readHead += size + sizeof(int);
            return true;
        }


        private unsafe void ProcessBatch(byte[] buf, int offset)
        {
            GetResponseObject();

            fixed (byte* b = &buf[offset])
            {
                byte* d = responseObject.obj.bufferPtr;
                var dend = d + responseObject.obj.buffer.Length;
                dcurr = d + sizeof(int); // reserve space for size
                int origPendingSeqNo = pendingSeqNo;

                var src = b;
                ref var header = ref Unsafe.AsRef<BatchHeader>(src);
                var num = header.NumMessages;
                src += BatchHeader.Size;
                Status status = default;

                dcurr += BatchHeader.Size;
                start = 0;
                msgnum = 0;

                for (msgnum = 0; msgnum < num; msgnum++)
                {
                    var message = (MessageType)(*src++);
                    switch (message)
                    {
                        case MessageType.Upsert:
                        case MessageType.UpsertAsync:
                            if ((int)(dend - dcurr) < 2)
                                SendAndReset(ref d, ref dend);

                            var keyPtr = src;
                            status = session.Upsert(ref serializer.ReadKeyByRef(ref src), ref serializer.ReadValueByRef(ref src));
                            hrw.Write(message, ref dcurr, (int)(dend - dcurr));
                            Write(ref status, ref dcurr, (int)(dend - dcurr));

                            subscribeKVBroker.Publish(keyPtr);
                            break;

                        case MessageType.Read:
                        case MessageType.ReadAsync:
                            if ((int)(dend - dcurr) < 2 + maxSizeSettings.MaxOutputSize)
                                SendAndReset(ref d, ref dend);

                            long ctx = ((long)message << 32) | (long)pendingSeqNo;
                            status = session.Read(ref serializer.ReadKeyByRef(ref src), ref serializer.ReadInputByRef(ref src),
                                ref serializer.AsRefOutput(dcurr + 2, (int)(dend - dcurr)), ctx, 0);

                            hrw.Write(message, ref dcurr, (int)(dend - dcurr));
                            Write(ref status, ref dcurr, (int)(dend - dcurr));

                            if (status == Status.PENDING)
                                Write(pendingSeqNo++, ref dcurr, (int)(dend - dcurr));
                            else if (status == Status.OK)
                                serializer.SkipOutput(ref dcurr);
                            break;

                        case MessageType.RMW:
                        case MessageType.RMWAsync:
                            if ((int)(dend - dcurr) < 2 + maxSizeSettings.MaxOutputSize)
                                SendAndReset(ref d, ref dend);

                            keyPtr = src;

                            ctx = ((long)message << 32) | (long)pendingSeqNo;
                            status = session.RMW(ref serializer.ReadKeyByRef(ref src), ref serializer.ReadInputByRef(ref src),
                                ref serializer.AsRefOutput(dcurr + 2, (int)(dend - dcurr)), ctx);

                            hrw.Write(message, ref dcurr, (int)(dend - dcurr));
                            Write(ref status, ref dcurr, (int)(dend - dcurr));
                            if (status == Status.PENDING)
                                Write(pendingSeqNo++, ref dcurr, (int)(dend - dcurr));
                            else if (status == Status.OK || status == Status.NOTFOUND)
                                serializer.SkipOutput(ref dcurr);

                            subscribeKVBroker.Publish(keyPtr);
                            break;

                        case MessageType.Delete:
                        case MessageType.DeleteAsync:
                            if ((int)(dend - dcurr) < 2)
                                SendAndReset(ref d, ref dend);

                            keyPtr = src;

                            status = session.Delete(ref serializer.ReadKeyByRef(ref src));
                            hrw.Write(message, ref dcurr, (int)(dend - dcurr));
                            Write(ref status, ref dcurr, (int)(dend - dcurr));

                            subscribeKVBroker.Publish(keyPtr);
                            break;

                        case MessageType.SubscribeKV:
                            if ((int)(dend - dcurr) < 2 + maxSizeSettings.MaxOutputSize)
                                SendAndReset(ref d, ref dend);

                            var keyStart = src;
                            serializer.ReadKeyByRef(ref src);

                            int sid = subscribeKVBroker.Subscribe(ref keyStart, this);
                            status = Status.PENDING;
                            hrw.Write(message, ref dcurr, (int)(dend - dcurr));
                            Write(ref status, ref dcurr, (int)(dend - dcurr));
                            Write(sid, ref dcurr, (int)(dend - dcurr));
                            break;

                        case MessageType.PSubscribeKV:
                            if ((int)(dend - dcurr) < 2 + maxSizeSettings.MaxOutputSize)
                                SendAndReset(ref d, ref dend);

                            keyStart = src;
                            serializer.ReadKeyByRef(ref src);

                            sid = subscribeKVBroker.PSubscribe(ref keyStart, this);
                            status = Status.PENDING;
                            hrw.Write(message, ref dcurr, (int)(dend - dcurr));
                            Write(ref status, ref dcurr, (int)(dend - dcurr));
                            Write(sid, ref dcurr, (int)(dend - dcurr));
                            break;

                        default:
                            throw new NotImplementedException();
                    }
                }

                if (origPendingSeqNo != pendingSeqNo)
                    session.CompletePending(true);

                // Send replies
                if (msgnum - start > 0)
                    Send(d);
                else
                    responseObject.Dispose();
            }
        }

        public unsafe override void Publish(ref byte* keyPtr, int keyLength, int sid, bool prefix)
        {
            Input input = default;
            MessageType message = MessageType.SubscribeKV;
            if (prefix)
                message = MessageType.PSubscribeKV;

            GetResponseObject();

            ref Key key = ref serializer.ReadKeyByRef(ref keyPtr);

            byte* d = responseObject.obj.bufferPtr;
            var dend = d + responseObject.obj.buffer.Length;
            dcurr = d + sizeof(int); // reserve space for size
            byte* outputDcurr;

            dcurr += BatchHeader.Size;
            start = 0;
            msgnum = 0;

            if ((int)(dend - dcurr) < 6 + maxSizeSettings.MaxOutputSize)
                SendAndReset(ref d, ref dend);

            long ctx = ((long)message << 32) | (long)sid;

            if (prefix)
                outputDcurr = dcurr + 6 + keyLength;
            else
                outputDcurr = dcurr + 6;

            var status = session.Read(ref key, ref input, ref serializer.AsRefOutput(outputDcurr, (int)(dend - dcurr)), ctx, 0);
            msgnum++;

            if (status != Status.PENDING)
            {
                // Write six bytes (message | status | sid)
                hrw.Write(message, ref dcurr, (int)(dend - dcurr));
                Write(ref status, ref dcurr, (int)(dend - dcurr));
                Write(sid, ref dcurr, (int)(dend - dcurr));
                if (prefix)
                    serializer.Write(ref key, ref dcurr, (int)(dend - dcurr));

                if (status == Status.OK)
                    serializer.SkipOutput(ref dcurr);
            }

            // Send replies
            if (msgnum - start > 0)
                Send(d);
            else
                responseObject.Dispose();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private unsafe bool Write(ref Status s, ref byte* dst, int length)
        {
            if (length < 1) return false;
            *dst++ = (byte)s;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private unsafe bool Write(int seqNo, ref byte* dst, int length)
        {
            if (length < sizeof(int)) return false;
            *(int*)dst = seqNo;
            dst += sizeof(int);
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SendAndReset(ref byte* d, ref byte* dend)
        {
            Send(d);
            GetResponseObject();
            d = responseObject.obj.bufferPtr;
            dend = d + responseObject.obj.buffer.Length;
            dcurr = d + sizeof(int);
            start = msgnum;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Send(byte* d)
        {
            var dstart = d + sizeof(int);
            Unsafe.AsRef<BatchHeader>(dstart).NumMessages = msgnum - start;
            Unsafe.AsRef<BatchHeader>(dstart).SeqNo = seqNo++;
            int payloadSize = (int)(dcurr - d);
            // Set packet size in header
            *(int*)responseObject.obj.bufferPtr = -(payloadSize - sizeof(int));
            SendResponse(payloadSize);
            responseObject.obj = null;
        }

        public override void Dispose()
        {
            subscribeKVBroker.RemoveSubscription(this);
        }
    }
}
