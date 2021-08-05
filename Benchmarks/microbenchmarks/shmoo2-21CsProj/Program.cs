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

namespace ConsoleApplication
{
    public class Program
    {
        private static uint COPYLEN = 2050;
		private static uint ITERATION = 1000000;
        
        public static unsafe void TestMemCpy(int lowerlimit, int upperlimit)
        {
            Random rnd = new Random(1);
            ushort[] copyLen = new ushort[COPYLEN];
            
            for (int i = 0; i < COPYLEN; i++)
            {
                copyLen[i] = (ushort) rnd.Next(lowerlimit, upperlimit+1);
            }
            
            // Create two arrays of the same length.
            byte[] byteArray1 = new byte[upperlimit];
            byte[] byteArray2 = new byte[upperlimit];
			
		
            // Fill byteArray1 with 0 - (BUCKET_SIZE - 1).
            for (int i = 0; i < upperlimit; i++)
            {
                byteArray1[i] = (byte)i;
            }            
            
            // The following fixed statement pins the location of the source and
            // target objects in memory so that they will not be moved by garbage
            // collection.
            fixed (byte* pSource = byteArray1, pTarget = byteArray2)
            {    
                long begin = 0;
                long time = 0;
                int j = 0;

                begin = DateTime.Now.Ticks;
                for (uint i = 0; i < ITERATION; i++)
				{
                    for (j = 0; j < COPYLEN; j++)
                    {
                        Buffer.MemoryCopy(pSource, pTarget, upperlimit, copyLen[j]);
                    }
				}
				
                time = DateTime.Now.Ticks - begin;
                Console.WriteLine("memcpy array size bkt: " + copyLen[j-1].ToString() + " time: " + time.ToString());
                //Console.WriteLine("Source: " + BitConverter.ToString(byteArray1) + " Dest: " + BitConverter.ToString(byteArray2));

            }
        }
        
        public static void Main()
        {
            TestMemCpy(0, 8);
            TestMemCpy(9, 16);
            TestMemCpy(17, 32);
            TestMemCpy(33, 64);
            TestMemCpy(65, 128);
            TestMemCpy(129, 256);
            TestMemCpy(257, 512);
            TestMemCpy(513, 1024);
            TestMemCpy(1025, 2048);
            TestMemCpy(2049, 4096);
        }
    }
}
