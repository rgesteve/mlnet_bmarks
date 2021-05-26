/*****************************************************
reference: https://gist.github.com/thomaslevesque/6f653d8b3a82b1d038e1
           http://stackoverflow.com/questions/1897555/what-is-the-equivalent-of-memset-in-c
           http://www.abstractpath.com/2009/memcpy-in-c/
           https://msdn.microsoft.com/en-us/library/28k1s2k6.aspx
this micro benchmark test JIT_MemSet/JIT_MemCpy
*****************************************************/
using System;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
namespace ConsoleApplication
{
    public class Program
    {
        private static long ITERATION = 1000000000;
      
        [MethodImpl(MethodImplOptions.NoOptimization)]
        public static void TestSpanClear(int len)
        {
            Span<byte> byteSpan0 = new byte[len];

            long begin = 0;
            long time = 0;

            for (int i = 0; i < ITERATION; i++)
            {
                byteSpan0.Clear();
            }   

            begin = DateTime.Now.Ticks;
           
            for (long i = 0; i < ITERATION; i++)
            {
                byteSpan0.Clear();
            }
            time = DateTime.Now.Ticks - begin;
            Console.WriteLine("length: " + len.ToString() + " ITERATION: " + ITERATION.ToString() + " ticks: " + time.ToString() + " ticks/1m_ITERATION: " + (time/(ITERATION/1000000)).ToString());
           
        }
        
        public static int Main(string[] args)
        {
            TestSpanClear(8);
            TestSpanClear(16);
            TestSpanClear(32);
            TestSpanClear(64);
            TestSpanClear(128);
            TestSpanClear(256);
            TestSpanClear(512);
            TestSpanClear(1024);
            TestSpanClear(2048);
            TestSpanClear(4096);
            return 0;
        }
        
    }
}

