using NUnit.Framework;
using Reactive4.NET.utils;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Reactive4.NET.Test
{
    [TestFixture]
    public class MpscLinkedArrayQueueTest
    {
        [Test]
        [Timeout(5000)]
        public void Normal()
        {
            var q = new MpscLinkedArrayQueue<int>(4);

            int item;

            Assert.IsTrue(q.IsEmpty());
            Assert.IsFalse(q.Poll(out item));


            q.Offer(1);
            Assert.IsFalse(q.IsEmpty());
            q.Offer(2);
            Assert.IsFalse(q.IsEmpty());
            q.Offer(3);
            Assert.IsFalse(q.IsEmpty());
            q.Offer(4);
            Assert.IsFalse(q.IsEmpty());
            q.Offer(5);
            Assert.IsFalse(q.IsEmpty());
            q.Offer(6);
            Assert.IsFalse(q.IsEmpty());

            Assert.IsTrue(q.Poll(out item));
            Assert.IsFalse(q.IsEmpty());
            Assert.AreEqual(1, item);

            Assert.IsTrue(q.Poll(out item));
            Assert.IsFalse(q.IsEmpty());
            Assert.AreEqual(2, item);

            Assert.IsTrue(q.Poll(out item));
            Assert.IsFalse(q.IsEmpty());
            Assert.AreEqual(3, item);

            Assert.IsTrue(q.Poll(out item));
            Assert.IsFalse(q.IsEmpty());
            Assert.AreEqual(4, item);

            Assert.IsTrue(q.Poll(out item));
            Assert.IsFalse(q.IsEmpty());
            Assert.AreEqual(5, item);

            Assert.IsTrue(q.Poll(out item));
            Assert.IsTrue(q.IsEmpty());
            Assert.AreEqual(6, item);

            Assert.IsFalse(q.Poll(out item));
            Assert.IsTrue(q.IsEmpty());

            q.Offer(7);
            Assert.IsTrue(q.Poll(out item));
            Assert.IsTrue(q.IsEmpty());
            Assert.AreEqual(7, item);
        }

        [Test]
        [Timeout(5000)]
        public void SyncOffer()
        {
            var q = new MpscLinkedArrayQueue<int>(4);
            for (int i = 0; i < 1000000; i++)
            {
                q.Offer(i);
            }
            for (int i = 0; i < 1000000; i++)
            {
                while (!q.Poll(out int item)) ;
            }

            Assert.IsTrue(q.IsEmpty());
        }

        [Test]
        [Timeout(5000)]
        public void AsyncOffer()
        {
            var q = new MpscLinkedArrayQueue<int>(4);

            Task.Factory.StartNew(() =>
            {
                for (int i = 0; i < 1000000; i++)
                {
                    q.Offer(i);
                }
            }, TaskCreationOptions.LongRunning);

            for (int i = 0; i < 1000000; i++)
            {
                if (i % 1000 == 0)
                {
                    Console.WriteLine("Read " + i);
                }
                while (!q.Poll(out int item)) ;
            }

            Assert.IsTrue(q.IsEmpty());
        }

        [Test]
        [Timeout(50000)]
        public void MultiOffer()
        {
            var q = new MpscLinkedArrayQueue<int>(4);

            int[] sync = { 2 };

            Task.Factory.StartNew(() =>
            {
                Interlocked.Decrement(ref sync[0]);
                while (Volatile.Read(ref sync[0]) != 0) ;

                for (int i = 0; i < 500000; i++)
                {
                    if (i % 50000 == 0)
                    {
                        Console.WriteLine("Written " + i);
                    }
                    q.Offer(i);
                }
                Console.WriteLine("Written " + 500000);
            }, TaskCreationOptions.LongRunning);

            Task.Factory.StartNew(() =>
            {
                Interlocked.Decrement(ref sync[0]);
                while (Volatile.Read(ref sync[0]) != 0) ;

                for (int i = 500000; i < 1000000; i++)
                {
                    if (i % 50000 == 0)
                    {
                        Console.WriteLine("Written " + i);
                    }
                    q.Offer(i);
                }
                Console.WriteLine("Written " + 1000000);
            }, TaskCreationOptions.LongRunning);

            for (int i = 0; i < 1000000; i++)
            {
                if (i % 10000 == 0)
                {
                    Console.WriteLine("Read " + i);
                }
                while (!q.Poll(out int item)) ;
            }

            Assert.IsTrue(q.IsEmpty());
        }

        [Test]
        [Timeout(5000)]
        public void MultiOffer2()
        {
            var q = new MpscLinkedArrayQueue<int>(4);

            int[] sync = { 2 };

            CountdownEvent latch = new CountdownEvent(2);

            Task.Factory.StartNew(() =>
            {
                Interlocked.Decrement(ref sync[0]);
                while (Volatile.Read(ref sync[0]) != 0) ;

                for (int i = 0; i < 500000; i++)
                {
                    if (i % 50000 == 0)
                    {
                        Console.WriteLine("Written " + i);
                    }
                    q.Offer(i);
                }
                Console.WriteLine("Written " + 500000);
                latch.Signal();
            }, TaskCreationOptions.LongRunning);

            Task.Factory.StartNew(() =>
            {
                Interlocked.Decrement(ref sync[0]);
                while (Volatile.Read(ref sync[0]) != 0) ;

                for (int i = 500000; i < 1000000; i++)
                {
                    if (i % 50000 == 0)
                    {
                        Console.WriteLine("Written " + i);
                    }
                    q.Offer(i);
                }
                Console.WriteLine("Written " + 1000000);
                latch.Signal();
            }, TaskCreationOptions.LongRunning);

            latch.Wait(5000);

            for (int i = 0; i < 1000000; i++)
            {
                if (i % 10000 == 0)
                {
                    Console.WriteLine("Read " + i);
                }
                if (!q.Poll(out int item))
                {
                    Assert.Fail("Queue appears to be empty?!");
                }
            }

            Assert.IsTrue(q.IsEmpty());
        }
    }
}
