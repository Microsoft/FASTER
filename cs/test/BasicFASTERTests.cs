﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

using System;
using System.Linq;
using FASTER.core;
using NUnit.Framework;
using System.IO;

namespace FASTER.test
{
    //** NOTE - more detailed / in depth Read tests in ReadAddressTests.cs 
    //** These tests ensure the basics are fully covered

    [TestFixture]
    internal class BasicFASTERTests
    {
        private FasterKV<KeyStruct, ValueStruct> fht;
        private ClientSession<KeyStruct, ValueStruct, InputStruct, OutputStruct, Empty, Functions> session;
        private IDevice log;
        private string path;
        TestUtils.DeviceType deviceType;


        [SetUp]
        public void Setup()
        {
            path = TestUtils.MethodTestDir + "/";

            // Clean up log files from previous test runs in case they weren't cleaned up
            TestUtils.DeleteDirectory(path, wait: true);

        }

        [TearDown]
        public void TearDown()
        {
            session?.Dispose();
            session = null;
            fht?.Dispose();
            fht = null;
            log?.Dispose();
            log = null;
            TestUtils.DeleteDirectory(path);
        }

        [Test]
        [Category("FasterKV")]
        [Category("Smoke")]
        public void NativeInMemWriteRead([Values] TestUtils.DeviceType deviceType)
        {

            string filename = path + "NativeInMemWriteRead" + deviceType.ToString() + ".log";
            log = TestUtils.CreateTestDevice(deviceType, filename);
            fht = new FasterKV<KeyStruct, ValueStruct>
                (128, new LogSettings { LogDevice = log, PageSizeBits = 10, MemorySizeBits = 12, SegmentSizeBits = 22 });
            session = fht.For(new Functions()).NewSession<Functions>();

            InputStruct input = default;
            OutputStruct output = default;

            var key1 = new KeyStruct { kfield1 = 13, kfield2 = 14 };
            var value = new ValueStruct { vfield1 = 23, vfield2 = 24 };

            session.Upsert(ref key1, ref value, Empty.Default, 0);
            var status = session.Read(ref key1, ref input, ref output, Empty.Default, 0);

            if (status == Status.PENDING)
            {
                session.CompletePending(true);
            }
            else
            {
                Assert.IsTrue(status == Status.OK);
            }

            Assert.IsTrue(output.value.vfield1 == value.vfield1);
            Assert.IsTrue(output.value.vfield2 == value.vfield2);
        }

        [Test]
        [Category("FasterKV")]
        [Category("Smoke")]
        public void NativeInMemWriteReadDelete([Values] TestUtils.DeviceType deviceType)
        {

            string filename = path + "NativeInMemWriteReadDelete" + deviceType.ToString() + ".log";
            log = TestUtils.CreateTestDevice(deviceType, filename);
            fht = new FasterKV<KeyStruct, ValueStruct>
                (128, new LogSettings { LogDevice = log, PageSizeBits = 10, MemorySizeBits = 12, SegmentSizeBits = 22 });
            session = fht.For(new Functions()).NewSession<Functions>();

            InputStruct input = default;
            OutputStruct output = default;

            var key1 = new KeyStruct { kfield1 = 13, kfield2 = 14 };
            var value = new ValueStruct { vfield1 = 23, vfield2 = 24 };

            session.Upsert(ref key1, ref value, Empty.Default, 0);
            var status = session.Read(ref key1, ref input, ref output, Empty.Default, 0);

            if (status == Status.PENDING)
            {
                session.CompletePending(true);
            }
            else
            {
                Assert.IsTrue(status == Status.OK);
            }

            session.Delete(ref key1, Empty.Default, 0);

            status = session.Read(ref key1, ref input, ref output, Empty.Default, 0);

            if (status == Status.PENDING)
            {
                session.CompletePending(true);
            }
            else
            {
                Assert.IsTrue(status == Status.NOTFOUND);
            }

            var key2 = new KeyStruct { kfield1 = 14, kfield2 = 15 };
            var value2 = new ValueStruct { vfield1 = 24, vfield2 = 25 };

            session.Upsert(ref key2, ref value2, Empty.Default, 0);
            status = session.Read(ref key2, ref input, ref output, Empty.Default, 0);

            if (status == Status.PENDING)
            {
                session.CompletePending(true);
            }
            else
            {
                Assert.IsTrue(status == Status.OK);
            }

            Assert.IsTrue(output.value.vfield1 == value2.vfield1);
            Assert.IsTrue(output.value.vfield2 == value2.vfield2);
        }


        [Test]
        [Category("FasterKV")]
        [Category("Smoke")]
        public void NativeInMemWriteReadDelete2()
        {

            // Just set this one since Write Read Delete already does all four devices
            deviceType = TestUtils.DeviceType.MLSD;

            const int count = 10;

            string filename = path + "NativeInMemWriteReadDelete2" + deviceType.ToString() + ".log";
            log = TestUtils.CreateTestDevice(deviceType, filename);
            fht = new FasterKV<KeyStruct, ValueStruct>
                //                (128, new LogSettings { LogDevice = log, MemorySizeBits = 22, SegmentSizeBits = 22, PageSizeBits = 10 });
                (128, new LogSettings { LogDevice = log, MemorySizeBits = 29 });
            session = fht.For(new Functions()).NewSession<Functions>();

            InputStruct input = default;
            OutputStruct output = default;

            for (int i = 0; i < 10 * count; i++)
            {
                var key1 = new KeyStruct { kfield1 = i, kfield2 = 14 };
                var value = new ValueStruct { vfield1 = i, vfield2 = 24 };

                session.Upsert(ref key1, ref value, Empty.Default, 0);
            }

            for (int i = 0; i < 10 * count; i++)
            {
                var key1 = new KeyStruct { kfield1 = i, kfield2 = 14 };
                session.Delete(ref key1, Empty.Default, 0);
            }

            for (int i = 0; i < 10 * count; i++)
            {
                var key1 = new KeyStruct { kfield1 = i, kfield2 = 14 };
                var value = new ValueStruct { vfield1 = i, vfield2 = 24 };

                var status = session.Read(ref key1, ref input, ref output, Empty.Default, 0);

                if (status == Status.PENDING)
                {
                    session.CompletePending(true);
                }
                else
                {
                    Assert.IsTrue(status == Status.NOTFOUND);
                }

                session.Upsert(ref key1, ref value, Empty.Default, 0);
            }

            for (int i = 0; i < 10 * count; i++)
            {
                var key1 = new KeyStruct { kfield1 = i, kfield2 = 14 };

                var status = session.Read(ref key1, ref input, ref output, Empty.Default, 0);

                if (status == Status.PENDING)
                {
                    session.CompletePending(true);
                }
                else
                {
                    Assert.IsTrue(status == Status.OK);
                }
            }
        }

        [Test]
        [Category("FasterKV")]
        [Category("Smoke")]
        public unsafe void NativeInMemWriteRead2()
        {
            // Just use this one instead of all four devices since InMemWriteRead covers all four devices
            deviceType = TestUtils.DeviceType.MLSD;

            int count = 200;

            string filename = path + "NativeInMemWriteRead2" + deviceType.ToString() + ".log";
            log = TestUtils.CreateTestDevice(deviceType, filename);
            fht = new FasterKV<KeyStruct, ValueStruct>
                //                (128, new LogSettings { LogDevice = log, MemorySizeBits = 22, SegmentSizeBits = 22, PageSizeBits = 10 });
                (128, new LogSettings { LogDevice = log, MemorySizeBits = 29 });
            session = fht.For(new Functions()).NewSession<Functions>();

            InputStruct input = default;

            Random r = new Random(10);
            for (int c = 0; c < count; c++)
            {
                var i = r.Next(10000);
                var key1 = new KeyStruct { kfield1 = i, kfield2 = i + 1 };
                var value = new ValueStruct { vfield1 = i, vfield2 = i + 1 };
                session.Upsert(ref key1, ref value, Empty.Default, 0);
            }

            r = new Random(10);

            for (int c = 0; c < count; c++)
            {
                var i = r.Next(10000);
                OutputStruct output = default;
                var key1 = new KeyStruct { kfield1 = i, kfield2 = i + 1 };
                var value = new ValueStruct { vfield1 = i, vfield2 = i + 1 };

                if (session.Read(ref key1, ref input, ref output, Empty.Default, 0) == Status.PENDING)
                {
                    session.CompletePending(true);
                }

                Assert.IsTrue(output.value.vfield1 == value.vfield1);
                Assert.IsTrue(output.value.vfield2 == value.vfield2);
            }

            // Clean up and retry - should not find now
            fht.Log.ShiftBeginAddress(fht.Log.TailAddress);

            r = new Random(10);
            for (int c = 0; c < count; c++)
            {
                var i = r.Next(10000);
                OutputStruct output = default;
                var key1 = new KeyStruct { kfield1 = i, kfield2 = i + 1 };
                Assert.IsTrue(session.Read(ref key1, ref input, ref output, Empty.Default, 0) == Status.NOTFOUND);
            }
        }

        [Test]
        [Category("FasterKV")]
        [Category("Smoke")]
        public unsafe void TestShiftHeadAddress([Values] TestUtils.DeviceType deviceType)
        {
            InputStruct input = default;
            int count = 200;

            string filename = path + "TestShiftHeadAddress" + deviceType.ToString() + ".log";
            log = TestUtils.CreateTestDevice(deviceType, filename);
            fht = new FasterKV<KeyStruct, ValueStruct>
                (128, new LogSettings { LogDevice = log, MemorySizeBits = 22, SegmentSizeBits = 22, PageSizeBits = 10 });
            session = fht.For(new Functions()).NewSession<Functions>();


            Random r = new Random(10);
            for (int c = 0; c < count; c++)
            {
                var i = r.Next(10000);
                var key1 = new KeyStruct { kfield1 = i, kfield2 = i + 1 };
                var value = new ValueStruct { vfield1 = i, vfield2 = i + 1 };
                session.Upsert(ref key1, ref value, Empty.Default, 0);
            }

            r = new Random(10);

            for (int c = 0; c < count; c++)
            {
                var i = r.Next(10000);
                OutputStruct output = default;
                var key1 = new KeyStruct { kfield1 = i, kfield2 = i + 1 };
                var value = new ValueStruct { vfield1 = i, vfield2 = i + 1 };

                if (session.Read(ref key1, ref input, ref output, Empty.Default, 0) == Status.PENDING)
                {
                    session.CompletePending(true);
                }

                Assert.IsTrue(output.value.vfield1 == value.vfield1, "output1:" + output.value.vfield1.ToString() + " value1:" + value.vfield1.ToString());
                Assert.IsTrue(output.value.vfield2 == value.vfield2, "output2:" + output.value.vfield2.ToString() + " value2:" + value.vfield2.ToString());
            }

            // Shift head and retry - should not find in main memory now
            fht.Log.FlushAndEvict(true);

            r = new Random(10);
            for (int c = 0; c < count; c++)
            {
                var i = r.Next(10000);
                OutputStruct output = default;
                var key1 = new KeyStruct { kfield1 = i, kfield2 = i + 1 };
                Status foundStatus = session.Read(ref key1, ref input, ref output, Empty.Default, 0);
                Assert.IsTrue(foundStatus == Status.PENDING, "Found Status:" + foundStatus.ToString() + " Expected Status: PENDING");
                session.CompletePending(true);
            }
        }

        [Test]
        [Category("FasterKV")]
        [Category("Smoke")]
        public unsafe void NativeInMemRMWRefKeys([Values] TestUtils.DeviceType deviceType)
        {
            InputStruct input = default;
            OutputStruct output = default;

            string filename = path + "NativeInMemRMWRefKeys" + deviceType.ToString() + ".log";
            log = TestUtils.CreateTestDevice(deviceType, filename);
            fht = new FasterKV<KeyStruct, ValueStruct>
                (128, new LogSettings { LogDevice = log, MemorySizeBits = 22, SegmentSizeBits = 22, PageSizeBits = 10 });
            session = fht.For(new Functions()).NewSession<Functions>();

            var nums = Enumerable.Range(0, 1000).ToArray();
            var rnd = new Random(11);
            for (int i = 0; i < nums.Length; ++i)
            {
                int randomIndex = rnd.Next(nums.Length);
                int temp = nums[randomIndex];
                nums[randomIndex] = nums[i];
                nums[i] = temp;
            }

            for (int j = 0; j < nums.Length; ++j)
            {
                var i = nums[j];
                var key1 = new KeyStruct { kfield1 = i, kfield2 = i + 1 };
                input = new InputStruct { ifield1 = i, ifield2 = i + 1 };
                session.RMW(ref key1, ref input, Empty.Default, 0);
            }
            for (int j = 0; j < nums.Length; ++j)
            {
                var i = nums[j];
                var key1 = new KeyStruct { kfield1 = i, kfield2 = i + 1 };
                input = new InputStruct { ifield1 = i, ifield2 = i + 1 };
                if (session.RMW(ref key1, ref input, ref output, Empty.Default, 0) == Status.PENDING)
                {
                    session.CompletePending(true);
                }
                else
                {
                    Assert.AreEqual(2 * i, output.value.vfield1);
                    Assert.AreEqual(2 * (i + 1), output.value.vfield2);
                }
            }

            Status status;
            KeyStruct key;

            for (int j = 0; j < nums.Length; ++j)
            {
                var i = nums[j];

                key = new KeyStruct { kfield1 = i, kfield2 = i + 1 };
                ValueStruct value = new ValueStruct { vfield1 = i, vfield2 = i + 1 };

                status = session.Read(ref key, ref input, ref output, Empty.Default, 0);

                if (status == Status.PENDING)
                {
                    session.CompletePending(true);
                }
                else
                {
                    Assert.IsTrue(status == Status.OK);
                }
                Assert.IsTrue(output.value.vfield1 == 2 * value.vfield1, "found " + output.value.vfield1 + ", expected " + 2 * value.vfield1);
                Assert.IsTrue(output.value.vfield2 == 2 * value.vfield2);
            }

            key = new KeyStruct { kfield1 = nums.Length, kfield2 = nums.Length + 1 };
            status = session.Read(ref key, ref input, ref output, Empty.Default, 0);

            if (status == Status.PENDING)
            {
                session.CompletePending(true);
            }
            else
            {
                Assert.IsTrue(status == Status.NOTFOUND);
            }
        }


        // Tests the overload where no reference params used: key,input,userContext,serialNo

        [Test]
        [Category("FasterKV")]
        public unsafe void NativeInMemRMWNoRefKeys([Values] TestUtils.DeviceType deviceType)
        {
            InputStruct input = default;

            string filename = path + "NativeInMemRMWNoRefKeys" + deviceType.ToString() + ".log";
            log = TestUtils.CreateTestDevice(deviceType, filename);
            fht = new FasterKV<KeyStruct, ValueStruct>
                (128, new LogSettings { LogDevice = log, MemorySizeBits = 22, SegmentSizeBits = 22, PageSizeBits = 10 });
            session = fht.For(new Functions()).NewSession<Functions>();

            var nums = Enumerable.Range(0, 1000).ToArray();
            var rnd = new Random(11);
            for (int i = 0; i < nums.Length; ++i)
            {
                int randomIndex = rnd.Next(nums.Length);
                int temp = nums[randomIndex];
                nums[randomIndex] = nums[i];
                nums[i] = temp;
            }

            for (int j = 0; j < nums.Length; ++j)
            {
                var i = nums[j];
                var key1 = new KeyStruct { kfield1 = i, kfield2 = i + 1 };
                input = new InputStruct { ifield1 = i, ifield2 = i + 1 };
                session.RMW(ref key1, ref input, Empty.Default, 0);
            }
            for (int j = 0; j < nums.Length; ++j)
            {
                var i = nums[j];
                var key1 = new KeyStruct { kfield1 = i, kfield2 = i + 1 };
                input = new InputStruct { ifield1 = i, ifield2 = i + 1 };
                session.RMW(key1, input);  // no ref and do not set any other params
            }

            OutputStruct output = default;
            Status status;
            KeyStruct key;

            for (int j = 0; j < nums.Length; ++j)
            {
                var i = nums[j];

                key = new KeyStruct { kfield1 = i, kfield2 = i + 1 };
                ValueStruct value = new ValueStruct { vfield1 = i, vfield2 = i + 1 };

                status = session.Read(ref key, ref input, ref output, Empty.Default, 0);

                if (status == Status.PENDING)
                {
                    session.CompletePending(true);
                }
                else
                {
                    Assert.IsTrue(status == Status.OK);
                }
                Assert.IsTrue(output.value.vfield1 == 2 * value.vfield1, "found " + output.value.vfield1 + ", expected " + 2 * value.vfield1);
                Assert.IsTrue(output.value.vfield2 == 2 * value.vfield2);
            }

            key = new KeyStruct { kfield1 = nums.Length, kfield2 = nums.Length + 1 };
            status = session.Read(ref key, ref input, ref output, Empty.Default, 0);

            if (status == Status.PENDING)
            {
                session.CompletePending(true);
            }
            else
            {
                Assert.IsTrue(status == Status.NOTFOUND);
            }
        }

        // Tests the overload of .Read(key, input, out output,  context, serialNo)
        [Test]
        [Category("FasterKV")]
        [Category("Smoke")]
        public void ReadNoRefKeyInputOutput([Values] TestUtils.DeviceType deviceType)
        {
            InputStruct input = default;
            OutputStruct output = default;

            string filename = path + "ReadNoRefKeyInputOutput" + deviceType.ToString() + ".log";
            log = TestUtils.CreateTestDevice(deviceType, filename);
            fht = new FasterKV<KeyStruct, ValueStruct>
                (128, new LogSettings { LogDevice = log, MemorySizeBits = 22, SegmentSizeBits = 22, PageSizeBits = 10 });
            session = fht.For(new Functions()).NewSession<Functions>();

            var key1 = new KeyStruct { kfield1 = 13, kfield2 = 14 };
            var value = new ValueStruct { vfield1 = 23, vfield2 = 24 };

            session.Upsert(ref key1, ref value, Empty.Default, 0);
            var status = session.Read(key1, input, out output, Empty.Default, 111);

            if (status == Status.PENDING)
            {
                session.CompletePending(true);
            }
            else
            {
                Assert.IsTrue(status == Status.OK);
            }

            // Verify the read data
            Assert.IsTrue(output.value.vfield1 == value.vfield1);
            Assert.IsTrue(output.value.vfield2 == value.vfield2);
            Assert.IsTrue(13 == key1.kfield1);
            Assert.IsTrue(14 == key1.kfield2);
        }


        // Test the overload call of .Read (key, out output, userContext, serialNo)
        [Test]
        [Category("FasterKV")]
        public void ReadNoRefKey([Values] TestUtils.DeviceType deviceType)
        {
            string filename = path + "ReadNoRefKey" + deviceType.ToString() + ".log";
            log = TestUtils.CreateTestDevice(deviceType, filename);
            fht = new FasterKV<KeyStruct, ValueStruct>
                (128, new LogSettings { LogDevice = log, MemorySizeBits = 22, SegmentSizeBits = 22, PageSizeBits = 10 });
            session = fht.For(new Functions()).NewSession<Functions>();

            OutputStruct output = default;

            var key1 = new KeyStruct { kfield1 = 13, kfield2 = 14 };
            var value = new ValueStruct { vfield1 = 23, vfield2 = 24 };

            session.Upsert(ref key1, ref value, Empty.Default, 0);
            var status = session.Read(key1, out output, Empty.Default, 1);

            if (status == Status.PENDING)
            {
                session.CompletePending(true);
            }
            else
            {
                Assert.IsTrue(status == Status.OK);
            }

            // Verify the read data
            Assert.IsTrue(output.value.vfield1 == value.vfield1);
            Assert.IsTrue(output.value.vfield2 == value.vfield2);
            Assert.IsTrue(13 == key1.kfield1);
            Assert.IsTrue(14 == key1.kfield2);
        }


        // Test the overload call of .Read (ref key, ref output, userContext, serialNo)
        [Test]
        [Category("FasterKV")]
        [Category("Smoke")]
        public void ReadWithoutInput([Values] TestUtils.DeviceType deviceType)
        {
            string filename = path + "ReadWithoutInput" + deviceType.ToString() + ".log";
            log = TestUtils.CreateTestDevice(deviceType, filename);
            fht = new FasterKV<KeyStruct, ValueStruct>
                (128, new LogSettings { LogDevice = log, MemorySizeBits = 22, SegmentSizeBits = 22, PageSizeBits = 10 });
            session = fht.For(new Functions()).NewSession<Functions>();

            OutputStruct output = default;

            var key1 = new KeyStruct { kfield1 = 13, kfield2 = 14 };
            var value = new ValueStruct { vfield1 = 23, vfield2 = 24 };

            session.Upsert(ref key1, ref value, Empty.Default, 0);
            var status = session.Read(ref key1, ref output, Empty.Default, 99);

            if (status == Status.PENDING)
            {
                session.CompletePending(true);
            }
            else
            {
                Assert.IsTrue(status == Status.OK);
            }

            // Verify the read data
            Assert.IsTrue(output.value.vfield1 == value.vfield1);
            Assert.IsTrue(output.value.vfield2 == value.vfield2);
            Assert.IsTrue(13 == key1.kfield1);
            Assert.IsTrue(14 == key1.kfield2);
        }


        // Test the overload call of .Read (ref key, ref input, ref output, ref recordInfo, userContext: context)
        [Test]
        [Category("FasterKV")]
        [Category("Smoke")]
        public void ReadWithoutSerialID()
        {
            // Just checking without Serial ID so one device type is enough
            deviceType = TestUtils.DeviceType.MLSD;

            string filename = path + "ReadWithoutSerialID" + deviceType.ToString() + ".log";
            log = TestUtils.CreateTestDevice(deviceType, filename);
            fht = new FasterKV<KeyStruct, ValueStruct>
                (128, new LogSettings { LogDevice = log, MemorySizeBits = 29 });
            session = fht.For(new Functions()).NewSession<Functions>();

            InputStruct input = default;
            OutputStruct output = default;

            var key1 = new KeyStruct { kfield1 = 13, kfield2 = 14 };
            var value = new ValueStruct { vfield1 = 23, vfield2 = 24 };

            session.Upsert(ref key1, ref value, Empty.Default, 0);
            var status = session.Read(ref key1, ref input, ref output, Empty.Default);

            if (status == Status.PENDING)
            {
                session.CompletePending(true);
            }
            else
            {
                Assert.IsTrue(status == Status.OK);
            }

            Assert.IsTrue(output.value.vfield1 == value.vfield1);
            Assert.IsTrue(output.value.vfield2 == value.vfield2);
            Assert.IsTrue(13 == key1.kfield1);
            Assert.IsTrue(14 == key1.kfield2);
        }


        // Test the overload call of .Read (key)
        [Test]
        [Category("FasterKV")]
        [Category("Smoke")]
        public void ReadBareMinParams([Values] TestUtils.DeviceType deviceType)
        {
            string filename = path + "ReadBareMinParams" + deviceType.ToString() + ".log";
            log = TestUtils.CreateTestDevice(deviceType, filename);
            fht = new FasterKV<KeyStruct, ValueStruct>
                (128, new LogSettings { LogDevice = log, MemorySizeBits = 22, SegmentSizeBits = 22, PageSizeBits = 10 });
            session = fht.For(new Functions()).NewSession<Functions>();

            var key1 = new KeyStruct { kfield1 = 13, kfield2 = 14 };
            var value = new ValueStruct { vfield1 = 23, vfield2 = 24 };

            session.Upsert(ref key1, ref value, Empty.Default, 0);

            var status = session.Read(key1);

            if (status.Item1 == Status.PENDING)
            {
                session.CompletePending(true);
            }
            else
            {
                Assert.IsTrue(status.Item1 == Status.OK);
            }

            Assert.IsTrue(status.Item2.value.vfield1 == value.vfield1);
            Assert.IsTrue(status.Item2.value.vfield2 == value.vfield2);
            Assert.IsTrue(13 == key1.kfield1);
            Assert.IsTrue(14 == key1.kfield2);
        }

        // Test the ReadAtAddress where ReadFlags = ReadFlags.none
        [Test]
        [Category("FasterKV")]
        [Category("Smoke")]
        public void ReadAtAddressReadFlagsNone()
        {
            // Just functional test of ReadFlag so one device is enough
            deviceType = TestUtils.DeviceType.MLSD;

            string filename = path + "ReadAtAddressReadFlagsNone" + deviceType.ToString() + ".log";
            log = TestUtils.CreateTestDevice(deviceType, filename);
            fht = new FasterKV<KeyStruct, ValueStruct>
                (128, new LogSettings { LogDevice = log, MemorySizeBits = 29 });
            session = fht.For(new Functions()).NewSession<Functions>();

            InputStruct input = default;
            OutputStruct output = default;

            var key1 = new KeyStruct { kfield1 = 13, kfield2 = 14 };
            var value = new ValueStruct { vfield1 = 23, vfield2 = 24 };
            var readAtAddress = fht.Log.BeginAddress;

            session.Upsert(ref key1, ref value, Empty.Default, 0);
            var status = session.ReadAtAddress(readAtAddress, ref input, ref output, ReadFlags.None, Empty.Default, 0);

            if (status == Status.PENDING)
            {
                session.CompletePending(true);
            }
            else
            {
                Assert.IsTrue(status == Status.OK);
            }

            Assert.IsTrue(output.value.vfield1 == value.vfield1);
            Assert.IsTrue(output.value.vfield2 == value.vfield2);
            Assert.IsTrue(13 == key1.kfield1);
            Assert.IsTrue(14 == key1.kfield2);
        }

        // Test the ReadAtAddress where ReadFlags = ReadFlags.SkipReadCache

        [Test]
        [Category("FasterKV")]
        [Category("Smoke")]
        public void ReadAtAddressReadFlagsSkipReadCache()
        {
            // Another ReadFlag functional test so one device is enough
            deviceType = TestUtils.DeviceType.MLSD;

            string filename = path + "ReadAtAddressReadFlagsSkipReadCache" + deviceType.ToString() + ".log";
            log = TestUtils.CreateTestDevice(deviceType, filename);
            fht = new FasterKV<KeyStruct, ValueStruct>
                (128, new LogSettings { LogDevice = log, MemorySizeBits = 29 });
            session = fht.For(new Functions()).NewSession<Functions>();

            InputStruct input = default;
            OutputStruct output = default;
            var key1 = new KeyStruct { kfield1 = 13, kfield2 = 14 };
            var value = new ValueStruct { vfield1 = 23, vfield2 = 24 };
            var readAtAddress = fht.Log.BeginAddress;

            session.Upsert(ref key1, ref value, Empty.Default, 0);
            //**** TODO: When Bug Fixed ... use the invalidAddress line
            // Bug #136259
            //        Ah—slight bug here.I took a quick look to verify that the logicalAddress passed to SingleReader was kInvalidAddress(0), 
            //and while I got that right for the SingleWriter call, I missed it on the SingleReader.
            //This is because we streamlined it to no longer expose RecordAccessor.IsReadCacheAddress, and I missed it here.
            //   test that the record retrieved on Read variants that do find the record in the readCache 
            //pass Constants.kInvalidAddress as the ‘address’ parameter to SingleReader.
            // Reading the same record using Read(…, ref RecordInfo…) 
            //and ReadFlags.SkipReadCache should return a valid record there. 
            //For now, write the same test, and instead of testing for address == kInvalidAddress, 
            //test for (address & Constants.kReadCacheBitMask) != 0.


            //var status = session.ReadAtAddress(invalidAddress, ref input, ref output, ReadFlags.SkipReadCache);
            var status = session.ReadAtAddress(readAtAddress, ref input, ref output, ReadFlags.SkipReadCache);

            if (status == Status.PENDING)
            {
                session.CompletePending(true);
            }
            else
            {
                Assert.IsTrue(status == Status.OK);
            }

            Assert.IsTrue(output.value.vfield1 == value.vfield1);
            Assert.IsTrue(output.value.vfield2 == value.vfield2);
            Assert.IsTrue(13 == key1.kfield1);
            Assert.IsTrue(14 == key1.kfield2);
        }

        // Simple Upsert test where ref key and ref value but nothing else set
        [Test]
        [Category("FasterKV")]
        [Category("Smoke")]
        public void UpsertDefaultsTest([Values] TestUtils.DeviceType deviceType)
        {
            string filename = path + "UpsertDefaultsTest" + deviceType.ToString() + ".log";
            log = TestUtils.CreateTestDevice(deviceType, filename);
            fht = new FasterKV<KeyStruct, ValueStruct>
                (128, new LogSettings { LogDevice = log, MemorySizeBits = 22, SegmentSizeBits = 22, PageSizeBits = 10 });
            session = fht.For(new Functions()).NewSession<Functions>();

            InputStruct input = default;
            OutputStruct output = default;

            var key1 = new KeyStruct { kfield1 = 13, kfield2 = 14 };
            var value = new ValueStruct { vfield1 = 23, vfield2 = 24 };

            Assert.IsTrue(fht.EntryCount == 0);

            session.Upsert(ref key1, ref value);
            var status = session.Read(ref key1, ref input, ref output, Empty.Default, 0);

            if (status == Status.PENDING)
            {
                session.CompletePending(true);
            }
            else
            {
                Assert.IsTrue(status == Status.OK);
            }

            Assert.IsTrue(fht.EntryCount == 1);
            Assert.IsTrue(output.value.vfield1 == value.vfield1);
            Assert.IsTrue(output.value.vfield2 == value.vfield2);
        }

        // Simple Upsert test of overload where not using Ref for key and value and setting all parameters
        [Test]
        [Category("FasterKV")]
        [Category("Smoke")]
        public void UpsertNoRefNoDefaultsTest()
        {
            // Just checking more parameter values so one device is enough
            deviceType = TestUtils.DeviceType.MLSD;

            string filename = path + "UpsertNoRefNoDefaultsTest" + deviceType.ToString() + ".log";
            log = TestUtils.CreateTestDevice(deviceType, filename);
            fht = new FasterKV<KeyStruct, ValueStruct>
              (128, new LogSettings { LogDevice = log, MemorySizeBits = 29 });
            session = fht.For(new Functions()).NewSession<Functions>();

            InputStruct input = default;
            OutputStruct output = default;

            var key1 = new KeyStruct { kfield1 = 13, kfield2 = 14 };
            var value = new ValueStruct { vfield1 = 23, vfield2 = 24 };

            session.Upsert(key1, value, Empty.Default, 0);
            var status = session.Read(ref key1, ref input, ref output, Empty.Default, 0);

            if (status == Status.PENDING)
            {
                session.CompletePending(true);
            }
            else
            {
                Assert.IsTrue(status == Status.OK);
            }

            Assert.IsTrue(output.value.vfield1 == value.vfield1);
            Assert.IsTrue(output.value.vfield2 == value.vfield2);
        }


        // Upsert Test using Serial Numbers ... based on the VersionedRead Sample
        [Test]
        [Category("FasterKV")]
        [Category("Smoke")]
        public void UpsertSerialNumberTest()
        {
            // Simple Upsert of Serial Number test so one device is enough
            deviceType = TestUtils.DeviceType.MLSD;

            string filename = path + "UpsertSerialNumberTest" + deviceType.ToString() + ".log";
            log = TestUtils.CreateTestDevice(deviceType, filename);
            fht = new FasterKV<KeyStruct, ValueStruct>
                (128, new LogSettings { LogDevice = log, MemorySizeBits = 29 });
            session = fht.For(new Functions()).NewSession<Functions>();

            int numKeys = 100;
            int keyMod = 10;
            int maxLap = numKeys / keyMod;
            InputStruct input = default;
            OutputStruct output = default;

            var value = new ValueStruct { vfield1 = 23, vfield2 = 24 };
            var key = new KeyStruct { kfield1 = 13, kfield2 = 14 };

            for (int i = 0; i < numKeys; i++)
            {
                // lap is used to illustrate the changing values
                var lap = i / keyMod;
                session.Upsert(ref key, ref value, serialNo: lap);
            }

            // Now verify 
            for (int j = 0; j < numKeys; j++)
            {
                var status = session.Read(ref key, ref input, ref output, serialNo: maxLap + 1);

                if (status == Status.PENDING)
                {
                    session.CompletePending(true);
                }
                else
                {
                    Assert.IsTrue(status == Status.OK);
                }

                Assert.IsTrue(output.value.vfield1 == value.vfield1);
                Assert.IsTrue(output.value.vfield2 == value.vfield2);
            }
        }

        //**** Quick End to End Sample code from help docs: https://microsoft.github.io/FASTER/docs/fasterkv-basics/  ***
        // Very minor changes to LogDevice call and type of Asserts to use but basically code from Sample code in docs
        // Also tests the overload call of .Read (ref key ref output) 
        [Test]
        [Category("FasterKV")]
        public static void KVBasicsSampleEndToEndInDocs()
        {
            string testDir = TestUtils.MethodTestDir;
            using var log = Devices.CreateLogDevice($"{testDir}/hlog.log", deleteOnClose: false);
            using var store = new FasterKV<long, long>(1L << 20, new LogSettings { LogDevice = log });
            using var s = store.NewSession(new SimpleFunctions<long, long>());
            long key = 1, value = 1, input = 10, output = 0;
            s.Upsert(ref key, ref value);
            s.Read(ref key, ref output);
            Assert.IsTrue(output == value);
            s.RMW(ref key, ref input);
            s.RMW(ref key, ref input);
            s.Read(ref key, ref output);
            Assert.IsTrue(output == 10);
        }
    }
}