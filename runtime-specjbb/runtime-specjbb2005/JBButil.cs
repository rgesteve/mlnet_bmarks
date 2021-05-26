/*
 *
 *
 *
 * Copyright (c) 2000-2005 Standard Performance Evaluation Corporation (SPEC)
 *
 * All rights reserved.
 *
 * Copyright (c) 1996-2005 IBM Corporation, Inc. All rights reserved.
 *
 */
using System;
using System.Runtime.InteropServices ;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Diagnostics;

// This is for calling GlobalMemoryStatus function to get 
	//1. Total Memory
	//2. Available Memory etc.
[StructLayout(LayoutKind.Sequential)]
public struct MemoryStatusEx
{
	public int length;
	public int memoryLoad;
	public long totalPhys;
	public long availPhys;
	public long totalPageFile;
	public long availPageFile;
	public long totalVirtual;
	public long availVirtual;
	public long ullAvailExtendedVirtual;
}


[StructLayout(LayoutKind.Sequential)]
public struct MemoryStatus
{
	public	int length;
	public	int memoryLoad;
	public	int totalPhys;
	public	int availPhys;
	public	int totalPageFile;
	public	int availPageFile;
	public	int totalVirtual;
	public	int availVirtual;
}

namespace Specjbb2005.src.spec.jbb
{
	/// <summary>
	/// Summary description for JBButil.
	/// </summary>
	public class JBButil
	{

		// This goes right after each class/interface statement
		//  static readonly String         COPYRIGHT         = "SPECjbb2005,"
		//  + "Copyright (c) 2000-2005 Standard Performance Evaluation Corporation (SPEC),"
		//  + "All rights reserved,"
		//  + "(C) Copyright IBM Corp., 1996 - 2005"
		//  + "All rights reserved,"
		//  + "Licensed Materials - Property of SPEC";

		[DllImport("Kernel32.dll")]
		private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx status) ;

		[DllImport("Kernel32.dll")]
		private static extern bool GlobalMemoryStatus(ref MemoryStatus status) ;

		private static readonly String originalText      = "ORIGINAL";

		private static Random       r;

		private static readonly String[] last_name_parts = 
	{
		"BAR", "OUGHT", "ABLE", "PRI", "PRES", "ESE", "ANTI", "CALLY",
		"ATION", "EING"
	};

		private static readonly char[]  alnum            = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ".ToCharArray();

		public static readonly short   A_C_LAST          = 255;                                            // range

		// is
		// [0..999]
		public static readonly short   C_C_LAST          = 173;                                            // within

		// [0..A]
		public static readonly short   A_C_ID            = 102;                                            // range

		// is
		// [1..3000]
		// //
		// should
		// be
		// 1023
		public static readonly short   C_C_ID            = 34;                                             // within

		// [0..A]
		// //
		// should
		// be
		// 342
		public static readonly short   A_OL_I_ID         = 8191;                                           // range

		// is
		// [1..100000]
		public static readonly short   C_OL_I_ID         = 5456;                                           // within

		// [0..A]
		public static readonly sbyte    MaxOrderLines     = 15;

		// WRR: Array for Random streams (per-warehouse)
		private static Random[]     warehouse_random_stream;

		private static TraceSwitch appLog=new TraceSwitch("TestSwitch", "SPECjbb.Switch");
		public static TraceSwitch getLog() 
		{
			return appLog;
		}
		public JBButil()
		{
			
		}

		public static long currentFreeMem() 
		{
			long result = 0;
            // UBUNTU
            return result;
			MemoryStatusEx mstatus = new MemoryStatusEx();
			mstatus.length = System.Runtime.InteropServices.Marshal.SizeOf(mstatus) ;
			GlobalMemoryStatusEx(ref mstatus) ;
			result = mstatus.availPhys ;//Runtime.getRuntime().freeMemory();
			return result;
		}

		public static long currentTotalMem() 
		{
			long result = 0;
            // UBUNTU
            return result;
            MemoryStatusEx mstatus = new MemoryStatusEx();
			mstatus.length = System.Runtime.InteropServices.Marshal.SizeOf(mstatus) ;
			GlobalMemoryStatusEx(ref mstatus) ;
			result = mstatus.totalPhys;//result = Runtime.getRuntime().totalMemory();
			return result;
		}

		public static long currentUsedMem() 
		{
			long result = 0;
			//result = Runtime.getRuntime().totalMemory();
			result = currentTotalMem() - currentFreeMem() ;
			return result;
		}

		// WRR: Added parameter so we know how many streams to make space for.
		public static void random_init(int num_warehouse_streams) 
		{
			r = new Random(Environment.TickCount);
			//r.setSeed(System.currentTimeMillis());
			// WRR: Allocate the streams array.
			warehouse_random_stream = new Random[num_warehouse_streams];
		}

		//The dotnet Randon function takes only int as the seed.
		public static void set_random_seed(long seed) 
		{
			r = new Random((int)seed) ;//r.setSeed(seed);
		}

		// WRR: Initializer for streams seeded from the base stream.
		public static Random derived_random_init(short warehouseId) 
		{
			Random r1;
			r1 = new Random((r.Next() & 0x7fffffff) * warehouseId);
			//r1.setSeed((long) (r.nextInt() & 0x7fffffff) * (long) warehouseId);
			return r1;
		}

		// WRR: Register per-warehouse Random stream so we can find it.
		public static void register_warehouse_Random_stream(short warehouseId,
			Random per_wh_r) 
		{
			if (warehouse_random_stream[warehouseId] != null) 
			{
				Console.WriteLine("Warning:  reregistering Random stream for warehouse "
						   + warehouseId);
			}
			warehouse_random_stream[warehouseId] = per_wh_r;
		}

		public static int random(int low, int high) 
		{
			return ((r.Next() & 0x7fffffff) % (high - low + 1)) + low;
		}

		// WRR: Takes a Random stream as input.
		public static int random(int low, int high, Random r) 
		{
			return ((r.Next() & 0x7fffffff) % (high - low + 1)) + low;
		}

		// WRR: Takes warehouseId as input.
		public static int random(int low, int high, short warehouseId) 
		{
			Random r = warehouse_random_stream[warehouseId];
			return ((r.Next() & 0x7fffffff) % (high - low + 1)) + low;
		}

		public static char[] create_random_a_string(int length_lo, int length_hi) 
		{
			int i, actual_length, aRandInt;
			actual_length = random(length_lo, length_hi);
			char[] temp = new char[actual_length];
			for (i = 0; i < actual_length; i++) 
			{
				aRandInt = random(0, 61);
				if (aRandInt > 61)
					aRandInt = 61;
				temp[i] = alnum[aRandInt];
			}
			return temp;
		}

		// WRR: Takes a Random stream as parameter.
		public static char[] create_random_a_string(int length_lo, int length_hi,
			Random r) 
		{
			int i, actual_length, aRandInt;
			actual_length = random(length_lo, length_hi, r);
			char[] temp = new char[actual_length];
			for (i = 0; i < actual_length; i++) 
			{
				aRandInt = random(0, 61, r);
				if (aRandInt > 61)
					aRandInt = 61;
				temp[i] = alnum[aRandInt];
			}
			return temp;
		}

		// WRR: Takes warehouseId as parameter.
		public static char[] create_random_a_string(int length_lo, int length_hi,
			short warehouseId) 
		{
			int i, actual_length, aRandInt;
			Random r = warehouse_random_stream[warehouseId];
			actual_length = random(length_lo, length_hi, r);
			char[] temp = new char[actual_length];
			for (i = 0; i < actual_length; i++) 
			{
				aRandInt = random(0, 61, r);
				if (aRandInt > 61)
					aRandInt = 61;
				temp[i] = alnum[aRandInt];
			}
			return temp;
		}

		public static char[] create_random_n_string(int length_lo, int length_hi) 
		{
			int i, actual_length;
			actual_length = random(length_lo, length_hi);
			char[] temp = new char[actual_length];
			for (i = 0; i < actual_length; i++) 
			{
				temp[i] = (char) random(48, 57);
			}
			return temp;
		}

		// WRR: Takes a Random stream as input.
		public static char[] create_random_n_string(int length_lo, int length_hi,
			Random r) 
		{
			int i, actual_length;
			actual_length = random(length_lo, length_hi, r);
			char[] temp = new char[actual_length];
			for (i = 0; i < actual_length; i++) 
			{
				temp[i] = (char) random(48, 57, r);
			}
			return temp;
		}

		// WRR: Takes warehouseId as input.
		public static char[] create_random_n_string(int length_lo, int length_hi,
			short warehouseId) 
		{
			int i, actual_length;
			Random r = warehouse_random_stream[warehouseId];
			actual_length = random(length_lo, length_hi, r);
			char[] temp = new char[actual_length];
			for (i = 0; i < actual_length; i++) 
			{
				temp[i] = (char) random(48, 57, r);
			}
			return temp;
		}

		public static float create_random_float_val_return(float val_lo,
			float val_hi, float precision) 
		{
			float f, result;
			f = (float) r.NextDouble() * (val_hi - val_lo) + val_lo;
			result = f - (float)(Math.IEEERemainder((double)f, (double)precision));
			return result;
		}

		// WRR: Takes a Random stream as input.
		public static float create_random_float_val_return(float val_lo,
			float val_hi, float precision, Random r) 
		{
			float f, result;
			f = (float) r.NextDouble() * (val_hi - val_lo) + val_lo;
			result = f - (float) (Math.IEEERemainder((double)f, (double)precision));
			return result;
		}

		public static float create_random_float_val_return(float val_lo,
			float val_hi, float precision, short warehouseId) 
		{
			float f, result;
			Random r = warehouse_random_stream[warehouseId];
			f = (float) r.NextDouble() * (val_hi - val_lo) + val_lo;
			result = f - (float) (Math.IEEERemainder((double)f, (double)precision));
			return result;
		}

		public static char[] create_a_string_with_original(int length_lo,
			int length_hi, float percent_to_set, int hit) 
		{
			long f;
			int actual_length, start_pos;
			char[] temp;
			int i;
			actual_length = random(length_lo, length_hi);
			temp = new char[actual_length];
			for (i = 0; i < actual_length; i++) 
			{
				temp[i] = (char) random(48, 57);
			}
			f = random(0, 100);
			if (f < percent_to_set) 
			{
				start_pos = random(0, temp.Length - 8);
				originalText.CopyTo(0, temp, start_pos, 8);//originalText.getChars(0, 8, temp, start_pos);
			}
			return temp;
		}
		
		public static char[] create_a_string_with_original(int length_lo,
			int length_hi, float percent_to_set, int hit, short warehouseId) 
		{
			long f;
			int actual_length, start_pos;
			char[] temp;
			int i;
			actual_length = random(length_lo, length_hi, warehouseId);
			temp = new char[actual_length];
			for (i = 0; i < actual_length; i++) 
			{
				temp[i] = (char) random(48, 57, warehouseId);
			}
			f = random(0, 100, warehouseId);
			if (f < percent_to_set) 
			{
				start_pos = random(0, temp.Length - 8, warehouseId);
				originalText.CopyTo(0, temp, start_pos, 8); //getChars(0, 8, temp, start_pos);
			}
			return temp;
		}

		public static String choose_random_last_name(int maxCustomers,
			short warehouseId) 
		{
			short customerID = create_random_customer_id(maxCustomers, warehouseId);
			String temp = create_random_last_name(customerID, warehouseId);
			return temp;
		}

		public static short create_random_customer_id(int maxCustomers,
			short warehouseId) 
		{
			int a_c_id = (JBButil.A_C_ID * maxCustomers) / 3000;
			int c_c_id = (JBButil.C_C_ID * maxCustomers) / 3000;
			short customerID = (short) JBButil.NUrand_val(a_c_id, 1, maxCustomers,
				c_c_id, warehouseId);
			return customerID;
		}

		public static int create_random_item_id(int maxItems, short warehouseId) 
		{
			int a_ol_i_id = (JBButil.A_OL_I_ID * maxItems) / 100000;
			int c_ol_i_id = (JBButil.C_OL_I_ID * maxItems) / 100000;
			int itemID;
			if (JBBmain.uniformRandomItems) 
			{
				itemID = random(1, maxItems, warehouseId);
			}
			else 
			{
				itemID = (int) JBButil.NUrand_val(a_ol_i_id, 1, maxItems,
					c_ol_i_id, warehouseId);
			}
			return itemID;
		}

		// WRR: per-warehouse Random stream version.
		public static String create_random_last_name(int cust_num, short warehouseId) 
		{
			int random_num;
			String temp;
			if ((cust_num == 0) || (cust_num > 1000)) 
			{
				random_num = NUrand_val(A_C_LAST, 0, 999, C_C_LAST,
					warehouse_random_stream[warehouseId]);
			}
			else 
			{
				random_num = cust_num - 1;
			}
			temp = last_name_parts[random_num / 100];
			random_num %= 100;
			temp = temp + last_name_parts[random_num / 10];
			random_num %= 10;
			temp = temp + last_name_parts[random_num];
			return temp;
		}

		// WRR: Takes a Random stream as input.
		public static int NUrand_val(int A, int x, int y, int C, Random r) 
		{
			return (((((random(0, A, r) | random(x, y, r)) + C) % (y - x + 1)) + x));
		}

		// WRR: Takes warehouseId as input.
		public static int NUrand_val(int A, int x, int y, int C, short warehouseId) 
		{
			Random r = warehouse_random_stream[warehouseId];
			return (((((random(0, A, r) | random(x, y, r)) + C) % (y - x + 1)) + x));
		}

		public static void milliSecondsToSleep(long mills) 
		{
			try 
			{
                // Thread.Sleep((int)mills);
                // CORECLR
                TimeSpan t = new TimeSpan(0, 0, 0, 0, (int) mills);
                System.Threading.Tasks.Task.Delay(t).Wait();
			}
			catch (Exception e) 
			{
				Trace.WriteLineIf(JBButil.getLog().TraceWarning,
					"  --> Exception: SLEEP INTERRUPTED! " + e.Message);
			}
		}

		public static void SecondsToSleep(long seconds) 
		{
			// convert seconds to milliseconds
			int mills = (int)(seconds * 1000);

			try 
			{
                // CORECLR Thread.Sleep(mills);

                TimeSpan t = new TimeSpan(0, 0, 0, 0, (int)mills);
                System.Threading.Tasks.Task.Delay(t).Wait();
            }
			catch (Exception e) 
			{
				Trace.WriteLineIf(JBButil.getLog().TraceWarning,
                    "  --> Exception: SLEEP INTERRUPTED! " + e.Message);
			}
		}

		public static void SecondsToSleep(double seconds) 
		{
			// convert seconds to milliseconds
			int mills = (int) (seconds * 1000.0d);
			try 
			{
                TimeSpan t = new TimeSpan(0, 0, 0, 0, (int)mills);
                System.Threading.Tasks.Task.Delay(t).Wait();
			}
			catch (Exception e) 
			{
				Trace.WriteLineIf(JBButil.getLog().TraceWarning,
                    "  --> Exception: SLEEP INTERRUPTED! " + e.Message);
			}
		}

		// WRR: Takes warehouseId as input.
		public static double negativeExpDistribution(double mean, short warehouseId) 
		{
			Random r = warehouse_random_stream[warehouseId];
			double t;
			double rf = r.NextDouble();
			double meanX10 = mean * 10;
			// 5.2.5.4
			t = (-Math.Log(rf)) * mean;
			if (t > meanX10)
				t = meanX10;
			return t;
		}

        private readonly static object _syncRoot = new Object(); // CORECLR

        // CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
        //public static void setLog(Logger appLog)
        public static void setLog(TraceSwitch appLog)
		{
            lock(_syncRoot)
			    JBButil.appLog = appLog;
		}

		
	}
}
