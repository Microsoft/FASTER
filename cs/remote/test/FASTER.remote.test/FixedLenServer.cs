﻿using FASTER.core;
using FASTER.server;
using FASTER.common;
using System;
using System.IO;

namespace FASTER.remote.test
{
    class FixedLenServer<Key, Value> : IDisposable
        where Key : unmanaged
        where Value : unmanaged
    {
        readonly string folderName;
        readonly FasterServer server;
        readonly FasterKV<Key, Value> store;

        public FixedLenServer(string folderName, Func<Value, Value, Value> merger, string address = "127.0.0.1", int port = 33278)
        {
            this.folderName = folderName;
            GetSettings(folderName, out var logSettings, out var checkpointSettings, out var indexSize);

            // We use blittable structs Key and Value to construct a costomized server for fixed-length types
            store = new FasterKV<Key, Value>(indexSize, logSettings, checkpointSettings);

            var broker = new SubscribeKVBroker<Key, Value, IKeySerializer<Key>>(new FixedLenKeySerializer<Key>());

            // Create session provider for FixedLen
            var provider = new FasterKVProvider<Key, Value, Value, Value, FixedLenServerFunctions<Key, Value>, FixedLenSerializer<Key, Value, Value, Value>>(store, e => new FixedLenServerFunctions<Key, Value>(merger), broker);
            
            server = new FasterServer(address, port);
            server.Register(WireFormat.DefaultFixedLenKV, provider);
            server.Start();
        }

        public void Dispose()
        {
            server.Dispose();
            store.Dispose();
            new DirectoryInfo(folderName).Delete(true);
        }

        private static void GetSettings(string LogDir, out LogSettings logSettings, out CheckpointSettings checkpointSettings, out int indexSize)
        {
            logSettings = new LogSettings { PreallocateLog = false };

            logSettings.PageSizeBits = 20;
            logSettings.MemorySizeBits = 25;
            logSettings.SegmentSizeBits = 30;
            indexSize = 1 << 20;

            var device = LogDir == "" ? new NullDevice() : Devices.CreateLogDevice(LogDir + "/hlog", preallocateFile: false);
            logSettings.LogDevice = device;

            string CheckpointDir = null;
            if (CheckpointDir == null && LogDir == null)
                checkpointSettings = null;
            else
                checkpointSettings = new CheckpointSettings
                {
                    CheckPointType = CheckpointType.FoldOver,
                    CheckpointDir = CheckpointDir ?? (LogDir + "/checkpoints")
                };
        }
    }

    sealed class FixedLenServerFunctions<Key, Value> : SimpleFunctions<Key, Value, long>
    {
        public FixedLenServerFunctions(Func<Value, Value, Value> merger) : base(merger)
        {
        }
    }
}
