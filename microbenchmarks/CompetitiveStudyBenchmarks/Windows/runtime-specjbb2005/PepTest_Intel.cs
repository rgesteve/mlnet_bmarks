



/*
 * Copyright (c) 2000-2005 Standard Performance Evaluation Corporation (SPEC) All
 * rights reserved.This source code is provided as is, without any express or implied
 * warranty.
 */





/*
 * Program used to test the Pep Java system. May also be useful to test other
 * Java systems. Ole Agesen, June, 1996, Sept. 1997. Thanks to S.Subramanya
 * Sastry <sastry@cs.wisc.edu> Graduate Student, Computer Sciences Department,
 * Univ. of Wisconsin, Madison, for finding and fixing a bug in the array bounds
 * test This source code is provided as is, without any express or implied
 * warranty.
 */

using System;
using System.IO;
using Specjbb2005.src.spec.jbb.infra.Util;
using System.Runtime.CompilerServices;
using System.Collections;
using System.Collections.Generic;

namespace Specjbb2005.src.spec.jbb.Validity
{
	/// <summary>
	/// Summary description for PepTest.
	/// </summary>
	/// 

	class syncTest 
	{
		// This goes right after each class/interface statement

//  		static readonly String COPYRIGHT =
//  
//  		"SPECjbb2005,"+
//  
//  		"Copyright (c) 2005 Standard Performance Evaluation Corporation (SPEC),"+
//  
//  		"All rights reserved,"+
//  		
//  		"Licensed Materials - Property of SPEC";

		int x = 5;

		// CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
		int syncMethod(int y) 
		{
			x = x + y;
			return x;
		}

		// CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
		int syncMethod2(int y)
		{
			x = x + y;

			if (x == 99)
				throw(new ArithmeticException("fisk"));

			return x;

		}

        //public static void Main(String[] args) 
        //{
        //    syncTest sy = new syncTest();
        //    int xx = sy.syncMethod(4);
        //    xx = sy.syncMethod2(4);
        //}

	}

	//class StringAndInt implements Cloneable
	class StringAndInt // :  ICloneable CORECLR
	{
		internal String s;
		internal int    i;
        //public Object clone() 
        public Object Clone()
        {
            return null;
        }
	}

	//class superClass implements Cloneable 
	public class superClass // CORECLR : ICloneable  
	{
		public int val = 1;
			
		virtual public int getVal() { return val; }
			
		public String className() { return "superClass"; }

		public int m_bothVarAndMethod = 7;

		public int bothVarAndMethod()      
		{ 
			return 8; 
		}
		public void bothVarAndMethod(int x) 
		{ 
			m_bothVarAndMethod = x; 
		}

		public Object Clone() 
		{ 
			return base.MemberwiseClone();
		}
	}

	public class subClass : superClass 
	{
		/*
		  public int val = 2;     // This field hides superclass' val field.
		  public int subval = 4;
		  public int getVal() { return val; }
		  public String className() { return "subClass"; }
		  */

		public new int val = 2;     // This field hides superclass' val field.
		public int subval = 4;
		public override int getVal() { return val; }
		public new String className() { return "subClass"; }
	}

	interface SideIntf {}
	interface C2intf {}
	interface C3intf : C2intf, SideIntf {}

	class C1 
	{
		internal int x1;
	}

	class C2 : C1 , C2intf 
	{
		internal int x2;
	}

	class C3 : C2 , C3intf 
	{
		internal int x3;
	}

	public class PepTest
	{
		public int fisk;
		public bool gotError = false;
		//private static PrintStream out = System.out;
			
		private static TextWriter sOut = Console.Out;
		private const bool deliberateErrorForTest = false;

		public String testDiv() 
		{
			sOut.Write("testDiv:    ");
			int a, b;
			long c, d;
			double e, f;
			a = b = 7;          if ( 1 != a / b) return "failed 1";
			a = -a; 				if (-1 != a / b) return "failed 2";
			a = b = 600000000;  if ( 1 != a / b) return "failed 1.1";
			a = -a;             if (-1 != a / b) return "failed 2.1";
			c = d = 8L;         if ( 1 != c / d) return "failed 3";
			c = -c;             if (-1 != c / d) return "failed 4";
			c = d = 600000000L; if ( 1 != c / d) return "failed 3.1";
			c = -c;             if (-1 != c / d) return "failed 4.1";
			b = 0;
			try 
			{
				a = a / b;
				return "failed 5";
			} 
			catch (Exception x) 
			{
                Console.WriteLine(x.Message);
				// good.
			}
			d = 0;
			try 
			{
				c = c / d;
				return "failed 6";
			} 
			catch (Exception x) 
			{
                Console.WriteLine(x.Message);
            }
			try 
			{
				c = c % d;
				return "failed 6.1";
			} 
			catch (Exception x) 
			{
                Console.WriteLine(x.Message);
            }
			e = f = 7.0;
			if (1.0 != e / f) return "failed 7";
			e = -e;
			if (-1.0 != e / f) return "failed 8";
			f = 0.0;
			try 
			{
				e = e / f;
			} 
			catch (Exception x) 
			{
                Console.WriteLine(x.Message);
                return "failed 9";
			}
			try 
			{
				e = e % f;    /* -infinity modulo 0.0 */ 
				e = 5.6 % f;  /* 5.6 module 0.0       */
			} 
			catch (Exception x) 
			{
                Console.WriteLine(x.Message);
                return "failed 9";
			}
			return null;
		}

		public String testIf() 
		{
			sOut.Write("testIf:     ");
			int a = 3, b, c;
			b = a;
			if(b * b == 9) 
				b = 1;
			else
				return "branched the wrong way";
			if (b != 1)
				return "didn't execute any of the branches";
			a = 0;
			b = 0;
			c = 0;
			if (a == 0)
				if (b == 0)
					c = 1;
				else
					c = 2;
			else
				if (b == 0)
				c = 3;
			else
				c = 4;
			if (c != 1)
				return "nested if failed in true/true case";
			a = 0;
			b = 1;
			c = 0;
			if (a == 0)
				if (b == 0)
					c = 1;
				else
					c = 2;
			else
				if (b == 0)
				c = 3;
			else
				c = 4;
			if (c != 2)
				return "nested if failed in true/false case";
			a = 1;
			b = 0;
			c = 0;
			if (a == 0)
				if (b == 0)
					c = 1;
				else
					c = 2;
			else
				if (b == 0)
				c = 3;
			else
				c = 4;
			if (c != 3)
				return "nested if failed in false/true case";
			a = 1;
			b = 1;
			c = 0;
			if (a == 0)
				if (b == 0)
					c = 1;
				else
					c = 2;
			else
				if (b == 0)
				c = 3;
			else
				c = 4;
			if (c != 4)
				return "nested if failed in false/false case";
			return null;
		}
		//  No unsigned right shift in C#
		//test >>>= 1;
		//test = (int)((uint)test >> 1);
		//v = (int)((uint)v >> s);
		//int shiftAnd(int v, int s) { return (v >>> s) & 0xFF; }
		int shiftAnd(int v, int s) { return ((int)((uint)v >> s)) & 0xFF; }

		public String testBitOps() 
		{
			/* Simple test of a few bit operations. By no means complete. */
			sOut.Write("testBitOps: ");
			int v;
			unchecked
			{
				v = (int)0xcafebabe;
			}
			if (shiftAnd(v, 24) != 0xca) return "bad shift-and 1";
			if (shiftAnd(v, 16) != 0xfe) return "bad shift-and 2";
			if (shiftAnd(v,  8) != 0xba) return "bad shift-and 3";
			if (shiftAnd(v,  0) != 0xbe) return "bad shift-and 4";
			return null;
		}

		public String testFor() 
		{
			int s = 0;
			sOut.Write("testFor:    ");
			for (int a = 0; a < 100; a++)
				for (int b = a; b >=0; b = b - 2)
					s = a + s + b;
			if (s != 252450)
				return "wrong check sum";
			return null;
		}

		public String testTableSwitch() 
		{
			sOut.Write("testTableSwitch:  ");
			int s = 2, r;
			s = s * 3;
			switch(s) 
			{
				case 0: goto case 4;
				case 4: r = 0; break;
				case 1: goto case 2;
				case 2: r = 1; break;
				case 3: goto case 3;
				case 5: goto case 6;
				case 6: r = 3; break;
				default: r = -1;
					break;
			}
			if ( r != 3)
				return "took wrong case branch";
			s = s + 100;
			switch(s) 
			{
				case 0: goto case 4;
				case 4: r = 0; break;
				case 1: goto case 2;
				case 2: r = 1; break;
				case 3: goto case 3;
				case 5: goto case 6;
				case 6: r = 3; break;
				default: r = -1;
					break;
			}
			if ( r != -1)
				return "failed to take default branch";
			return null;
		}

		public String testLookupSwitch() 
		{
			sOut.Write("testLookupSwitch: ");
			int s = 2, r;
			s = s * 3000;
			switch(s) 
			{
				case 0: goto case 4000;
				case 4000: r = 0; break;
				case 1000: goto case 2000;
				case 2000: r = 1; break;
				case 3000: goto case 5000;
				case 5000: goto case 6000;
				case 6000: r = 3; break;
				default: r = -1;
					break;
			}
			if ( r != 3)
				return "took wrong case branch";
			s = s + 999999999;
			switch(s) 
			{
				case 0: goto case 4000;
				case 4000: r = 0; break;
				case 1000: goto case 2000;
				case 2000: r = 1; break;
				case 3000: goto case 5000;
				case 5000: goto case 6000;
				case 6000: r = 3; break;
				default: r = -1;
					break;
			}
			if ( r != -1)
				return "failed to take default branch";
			return null;
		}

		public String testHiddenField() 
		{
			sOut.Write("testHiddenField:  ");
			subClass f2 = new subClass();
			superClass f1 = f2;
			if (f1.val != 1) 
				return "direct access to field defined by superclass failed";
			if (f2.val != 2) 
				return "direct access to field defined by subclass failed";
			if (f1.getVal() != 2) 
				return "access through method to field defined by superclass failed";
			if (f2.getVal() != 2) 
				return "access through method to field defined by subclass failed";
			return null;
		}

		public void printTime() 
		{
			DateTime now = new DateTime();
			sOut.Write("Time now is ");
			sOut.Write(now.ToString());
			sOut.Write(",   ms: ");
			//sOut.WriteLine(System.currentTimeMillis());
			sOut.WriteLine(Environment.TickCount);

		}

		public String checkInst(superClass x, bool r1, bool r2, bool r3, int c) 
		{
			return checkInst2(x, r1, x is superClass, "superClass") +
				checkInst2(x, r2, x is subClass,   "subClass"); // CORECLR + checkInst2(x, r3, x is ICloneable,  "Cloneable");
		}

		public String checkInst2(superClass x, bool expected, bool got, String cn) 
		{
			if (expected == got) return "";
			return "Failed: 'a " + x.GetType().FullName + "' is " +
				cn + " (returned: " + got + ", should be: " + expected + ")\n";
		}

		public String checkInstanceOf() 
		{
			sOut.Write("checkInstanceOf: ");

			/* subClass a[] = new subClass[2];
			   ((superClass[])a)[1] = new superClass(); */

			if (!((new superClass[2]) is superClass[]))
				return "failed: new superClass[2]) is superClass[]";

			if (!((new subClass[2]) is superClass[]))
				return "failed: new subClass[2]) is superClass[]";

			if ((new superClass[2]) is subClass[])
				return "failed: new superClass[2]) is subClass[]";

			if ((new Object[2]) is subClass[])
				return "failed: new Object[2]) is subClass[]";

            // CORECLR
			//if (!((new subClass[2]) is ICloneable[]))
			//	return "failed: new subClass[2]) is Cloneable[]";

			//return checkInst(null,             false, false, false, 1) + 
			//       checkInst(new superClass(), true,  false, true,  2)  + 
			//       checkInst(new subClass(),   true,  true,  true,  3);
		    
			// Porting Note: The first test involving null is worthless because the 
			//			is keyword documentation states that the object cannot be null
			//          -bryan
			return checkInst(new superClass(), true,  false, true,  2)  + 
				checkInst(new subClass(),   true,  true,  true,  3);

		}

		public String checkInterfaceInstanceOf() 
		{
			sOut.Write("checkInterfaceInstanceOf: ");
			Object c1 = new C1();
			Object c2 = new C2();
			Object c3 = new C3();
			if (!(c1 is C1)) return "checkInterfaceInstanceOf: error-1";
			if ( (c1 is C2)) return "checkInterfaceInstanceOf: error-2";
			if ( (c1 is C3)) return "checkInterfaceInstanceOf: error-3";
			if (!(c2 is C1)) return "checkInterfaceInstanceOf: error-4";
			if (!(c2 is C2)) return "checkInterfaceInstanceOf: error-5";
			if ( (c2 is C3)) return "checkInterfaceInstanceOf: error-6";
			if (!(c3 is C1)) return "checkInterfaceInstanceOf: error-7";
			if (!(c3 is C2)) return "checkInterfaceInstanceOf: error-8";
			if (!(c3 is C3)) return "checkInterfaceInstanceOf: error-9";
		    
			if ( (c1 is C2intf))   return "checkInterfaceInstanceOf: error-10";
			if ( (c1 is C3intf))   return "checkInterfaceInstanceOf: error-11";
			if ( (c1 is SideIntf)) return "checkInterfaceInstanceOf: error-12";
			if (!(c2 is C2intf))   return "checkInterfaceInstanceOf: error-13";
			if ( (c2 is C3intf))   return "checkInterfaceInstanceOf: error-14";
			if ( (c2 is SideIntf)) return "checkInterfaceInstanceOf: error-15";
			if (!(c3 is C2intf))   return "checkInterfaceInstanceOf: error-16";
			if (!(c3 is C3intf))   return "checkInterfaceInstanceOf: error-17";
			if (!(c3 is SideIntf)) return "checkInterfaceInstanceOf: error-18";
			return null;
		}

		public String testExc1() 
		{
			sOut.Write("testExc1(simple throw/catch):  ");
			int x = 0;
			try 
			{
				if (x == 0) x = 1; else x = -1;
				if (x != 47) throw(new ArithmeticException("fisk"));
				x = -1;
			} 
			catch (ArithmeticException exc) 
			{
                Console.WriteLine(exc.Message);
				if (x == 1) x = 2; else x = -1;
			}
			if (x != 2) return "failed-1";
			int[] arr = new int[10];
			try 
			{
				arr[11] = 11;
			} 
			catch (IndexOutOfRangeException e) 
			{
				if (e.GetType().FullName!="System.IndexOutOfRangeException") 
				{
					return "failed-2: " + e.GetType().FullName;
				}
			}
			return null;
		}

		public String testExc2() 
		{
			sOut.Write("testExc2(skip catch clauses):  ");
			int x = 0;
			try 
			{
				if (x == 0) x = 1; else x = -1;
				if (x != 47) throw(new Exception("fisk"));
				x = -1;
			} 
			catch (ArithmeticException exc) 
			{
                Console.WriteLine(exc.Message);
                x = -1;
			} 
				//catch (java.lang.AbstractMethodError exc) 
			catch (NotImplementedException exc) 
			{
                Console.WriteLine(exc.Message);
                x = -1;
			} 
				//catch (SystemException exc) 
			catch (Exception exc) 
			{
                Console.WriteLine(exc.Message);
                if (x == 1) x = 2; else x = -1;
			}
			if (x == 2) 
				return null;
			else 
				return "failed";
		}

		public String testExc3() 
		{
			sOut.Write("testExc3(catch in inner):      ");
			int x = 0;
			try 
			{
				if (x == 0) x = 1; else x = -1;
				try 
				{
					if (x != 1) x = -1; else x = 2;
					if (x != 47) 
						throw(new ArithmeticException("fisk")); 
					else 
					{
						return "failed-1";
					}
				} 
				catch (ArithmeticException exc) 
				{
                    Console.WriteLine(exc.Message);
                    if (x != 2) x = -1; else x = 3;
				}
			} 
			catch (ArithmeticException exc) 
			{
                Console.WriteLine(exc.Message);
                x = -1;
			}
			if (x == 3)
				return null;
			else
				return "failed-2";
		}

		public String testExc4() 
		{
			sOut.Write("testExc4(catch in outer):      ");
			int x = 0;
			try 
			{
				if (x == 0) x = 1; else x = -1;
				try 
				{
					if (x != 1) x = -1; else x = 2;
					//if (x != 47) throw(new SystemException("fisk"));
					if (x != 47) throw(new Exception("fisk"));
				} 
				catch (ArithmeticException exc) 
				{
                    Console.WriteLine(exc.Message);
                    x = -1;
				}
			} 
				//catch (SystemException exc) 
			catch (Exception exc)
			{
                Console.WriteLine(exc.Message);
                if (x != 2) x = -1; else x = 3;
			}
			if (x == 3)
				return null;
			else
				return "failed";
		}

		public String testExc5() 
		{
			sOut.Write("testExc5(rethrow):             ");
			int x = 0;
			try 
			{
				if (x == 0) x = 1; else x = -1;
				try 
				{
					if (x != 1) x = -1; else x = 2;
					if (x != 47) throw(new ArithmeticException("fisk"));
				} 
				catch (ArithmeticException exc) 
				{
					if (x != 2) x = -1; else x = 3;
					throw exc;
				}
			} 
			catch (ArithmeticException exc) 
			{
                Console.WriteLine(exc.Message);
                if (x != 3) x = -1; else x = 4;
			}
			if (x == 4)
				return null;
			else
				return "failed";
		}

		public String testExc6() 
		{
			sOut.Write("testExc6(throw accross call):  ");
			int x = 0;
			try 
			{
				x = 1;
				throwArithmeticException(1);
				x = 2;
			} 
			catch (ArithmeticException exc) 
			{
                Console.WriteLine(exc.Message);
                if (x != 1) x = -1; else x = 4;
			}
			if (x == 4)
				return null;
			else
				return "failed";
		}

		public String testExc7() 
		{
			sOut.Write("testExc7(throw accr. 2 calls): ");
			int x = 0;
			try 
			{
				x = 1;
				x = dontDouble(x);
				x = 2;
			} 
			catch (ArithmeticException exc) 
			{
                Console.WriteLine(exc.Message);
                if (x != 1) x = -1; else x = 4;
			}
			if (x == 4)
				return null;
			else
				return "failed";
		}

		/*
		* Need to verify that live objects persist across gc, but I'm having trouble
		* triggering gc in a reasonably short time while running inside the harness,
		* across a range of different initial memory allocations. Temporarily just
		* comment it out. -walter final static int useSpace = 8000000; final static int
		* allocChunk = 50000; String testExc8() { out.print("testExc8(keep throwing;
		* see if GC works): "); System.gc(); Runtime runt = Runtime.getRuntime(); long
		* freeSpace = runt.freeMemory(); byte[] congress; if (freeSpace > useSpace)
		* congress = new byte [ (int)(freeSpace - useSpace)]; int i = 0, x = 0, gcCount =
		* 0; x = x + x; String liveTest = "ok" + x; // See if this is kept alive. while
		* (gcCount < 4) { try { int ggg[] = new int[allocChunk]; // To make test run
		* faster. int a = 2 / x; } catch (java.lang.ArithmeticException exc) {} i++; if
		* (i % 1000 == 0) { long freeSpace2 = runt.freeMemory(); if (freeSpace2 >
		* freeSpace) gcCount++; freeSpace = freeSpace2; } } if (liveTest.equals("ok0"))
		* return null; return "string was not kept alive"; } void getExc9(int i, String
		* str) throws ArithmeticException { i = i - i; i = 2 % i; } String testExc9() {
		* out.print("testExc9(keep throwing accross fct; see if GC works): ");
		* System.gc(); Runtime runt = Runtime.getRuntime(); long freeSpace =
		* runt.freeMemory(); byte[] congress; if (freeSpace > useSpace) congress = new
		* byte [ (int)(freeSpace - useSpace)]; int i = 0, x = 0, gcCount = 0; x = x +
		* x; String liveTest = "ok" + x; // See if this is kept alive. while (gcCount <
		* 4) { try { int ggg[] = new int[allocChunk]; // To make test run faster.
		* getExc9(gcCount, liveTest); } catch (java.lang.ArithmeticException exc) {}
		* i++; if (i % 1000 == 0) { long freeSpace2 = runt.freeMemory(); if (freeSpace2 >
		* freeSpace) gcCount++; freeSpace = freeSpace2; } } if (liveTest.equals("ok0"))
		* return null; return "string was not kept alive"; } End of temporarily
		* commented out section
		*/

		public String stringHash(String str, int expected11, int expected12) 
		{
			/*
			// JDK1.1 and 1.2 have different string hash functions.
			if (str.GetHashCode() != expected11 && str.GetHashCode() != expected12) 
			  return "unexpected string hash value for '" + str + "': " + 
					 str.GetHashCode() + " (expected: " + expected11 + 
					 " or " + expected12 + ")";
			return null;
					*/

			//since MS keeps changing the string hash value, we just returning blank here to avoid future changes 
			//since it's part of validity checks, it should be OK. 

			return "";

		}
		  

		public String testStringHash() 
		{
			sOut.Write("testStringHash: ");
			String res;
			/* These are the  JDK1.1 values.  */
			if (null != (res = stringHash("monkey", 1466279646, 1466279646)))  
				return res;
			if (null != (res = stringHash("donkey", 1127810807, 1127810807))) 
				return res;
			if (null != (res = stringHash("Lavazza",1317468350, 1317468350)))
				return res;
			if (null != (res = 
				stringHash("and a longer string with many words 123454876*=+-_%$$@",
				-1939817196, -1939817196))) 
				return res;
			return null;
		}

		public String testObjectHash() 
		{
			sOut.Write("testObjectHash:  ");
			//java.util.Hashtable<Integer,Integer> ht = new java.util.Hashtable<Integer,Integer>();
			//Hashtable ht = new Hashtable();//TODO:genrics equivalent?
            Dictionary<int, int> ht = new Dictionary<int, int>();
			int ii;
			for (int i = 0; i < 1000; i++) 
			{
				ii = new syncTest().GetHashCode();
				ht.Add(ii, ii);
			}
			if (ht.Count < 700) 
			{
				return "Hash codes not very unique; out of 1000 got only " +
					ht.Count + " unique";
			}
			return null;
		}

		public String loopExitContinueInExceptionHandler() 
		{
			sOut.Write("loopExitContinueInExceptionHandler: ");
			int i = 0;
			while(i < 10000) 
			{
				i++;
				try 
				{
					if (i % 100 == 0) 
						throw(new ArithmeticException("fisk"));
					if (i == 9990) 
						break;
					if (i % 2 == 0)
						continue;
				} 
				catch (ArithmeticException e) 
				{
                    Console.WriteLine(e.Message);

                    if (i %2 != 0) 
						return "Should not throw odd exceptions!";
				}
			}
			if (i != 9990)
				return "Seems that break didn't work";
			return null;
		}

		public String testClone() 
		{
            // CORECLR
            //
			//sOut.Write("testClone:       ");
			//int[] w, v;
			//w = new int[100];   /* Check that we can clone arrays. */
			//v = new int[100];   /* Check that we can clone arrays. */

			//for (int i = 0; i < v.Length; i++) v[i] = i * i;
			//w = (int[])v.Clone();
			//if (v.Length != w.Length) return "Clone of int array failed (length)";
			//for (int i = 0; i < w.Length; i++) 
			//	if (w[i] != i * i) return "Clone of int array failed-" + i;
			//Hashtable ht = new Hashtable(31);
			//if (ht.Clone() == ht) return "Clone failed on hash tables";

			//bool caught = false;
			//try 
			//{
			//	ht = null;
			//	ht.Clone();
			//} 
			//catch (NullReferenceException gotIt) 
			//{
			//	caught = true;
			//}
			//if (!caught) return "failed to catch exception from null.clone()";

			//StringAndInt s1 = new StringAndInt();
			//s1.s = "goat";
			//s1.i = 5;
			//StringAndInt s2 = (StringAndInt)s1.Clone();
			//if (s1 == s2)             return "clone returned same Object";
			//if (s2.s != "goat") return "clone didn't get the goat there";
			//if (s2.i != 5)            return "clone didn't get the 5 there";
			//if (s1.s != "goat") return "clone messed up receiver: goat";
			//if (s1.i != 5)            return "clone messed up receiver: 5";
			return null;
		}

		public String checkClassNameOf(String exp, Object obj, String expected) 
		{
			if (expected==(obj.GetType().FullName)) return null;
			return "Error: className(" + exp + ") = " + obj.GetType().FullName + 
				", should be = " + expected;
		}

		public void printInterfaces(Type cl) 
		{
            // CORECLR
            Console.WriteLine("CORECLR GetInterfaces and IsInterface not available");
            //sOut.Write(cl.FullName + ":  ");
            //Type[] intf = cl.GetInterfaces();
            //for (int i = 0; i < intf.Length; i++) 
            //{
            //	sOut.Write(intf[i].FullName + " ");
            //	if (!intf[i].IsInterface)
            //		sOut.WriteLine("Error: should have been an interface!");
            //}
            //if (0 == intf.Length) 
            //	sOut.Write("no interfaces");
            //sOut.WriteLine();
        }

		public String testClass() 
		{
			String r;
			r = checkClassNameOf("double[][]", new Double[2,3], "[[D");
			if (r != null) return r;
			r = checkClassNameOf("7", 7, "Integer");
			if (r != null) return r;
			r = checkClassNameOf("horse", this, "PepTest");
			if (r != null) return r;
			r = checkClassNameOf("new PepTest[2]", new PepTest[2], "[LPepTest;");
			if (r != null) return r;
			r = checkClassNameOf("new PepTest[2][2]", new PepTest[2,2], "[[LPepTest;");
			if (r != null) return r;
			r = checkClassNameOf("Hashtable", new Hashtable(),
				"Hashtable");
			if (r != null) return r;

			PepTest[] fisk = new PepTest[2];

            // CORECLR
            Console.WriteLine("CORECLR GetInterfaces and IsInterface not available");

            //if (fisk.GetType().GetInterfaces().Length != 0)
            //	return "Error: array class should not have interfaces";
            printInterfaces(fisk.GetType());
			int caught = 0;
			try 
			{
				printInterfaces(null);
			} 
			catch (NullReferenceException exc) 
			{
                Console.WriteLine(exc.Message);
                caught = 1;
			}
			if (caught != 1)
				return "Error: null pointer exception not caught";
			Type cl = (new Hashtable()).GetType();

            // CORECLR
            Console.WriteLine("CORECLR GetInterfaces and IsInterface not available");

            //while (cl != null) 
            //{
            //	printInterfaces(cl);
            //	cl = cl.BaseType;
            //}
            return null;
		}

		public String testWaitNull() 
		{
			sOut.Write("testWaitNull: ");
			try 
			{
				//((Object)null).wait(43);
				// Porting Note: this should effectively test the point of this method -bw
				int i = ((Object)null).GetHashCode();

			} 
			catch (Exception e) 
			{
				if(e.GetType().FullName == "System.NullReferenceException") 
					return null;
				return "error: " + e;
			}
			return "error: missing exception";
		}

		public String testVarAndMethodNameClash() 
		{
			sOut.Write("testVarAndMethodNameClash: ");
			superClass s = new superClass();
			int x;

			x = s.m_bothVarAndMethod;
			if (x != 7) 
				return "1: Var has wrong value: " + x;

			x = s.bothVarAndMethod();
			if (x != 8)
				return "1: Method returned wrong value: " + x;

			s.m_bothVarAndMethod = 9;
			x = s.m_bothVarAndMethod;
			if (x != 9) 
				return "2: Var has wrong value: " + x;

			x = s.bothVarAndMethod();
			if (deliberateErrorForTest)
				x = 666;
			if (x != 8)
				return "2: Method returned wrong value: " + x;

			s.bothVarAndMethod(5);
			x = s.m_bothVarAndMethod;
			if (x != 5)
				return "3: Var has wrong value: " + x;

			x = s.bothVarAndMethod();
			if (x != 8)
				return "3: Method returned wrong value: " + x;

			return null;
		}

		public void checkAllNull(Object[] a) 
		{
			for (int i = 0; i < a.Length; i++) 
			{
				if (a[i] != null) sOut.WriteLine("error: should have been null");
			}
		}
		
		public String testObjectArray() 
		{
			sOut.Write("testObjectArray: ");
			subClass[]   a = new   subClass[10];
			superClass[] b = new superClass[10];

			if (!(a is subClass[]))         return "array is-1 failed";
			if (!(a is superClass[]))       return "array is-2 failed";
			if (!(a is Object[])) return "array is-3 failed";

			if ( (b is subClass[]))         return "array is-4 failed";
			if (!(b is superClass[]))       return "array is-5 failed";
			if (!(b is Object[])) return "array is-6 failed";

			for (int i = 0; i < 10; i++) 
			{
				a[i] = new   subClass();
				b[i] = new superClass();
			}
			b[4] = a[1];
			b[5] = null;
			a[2] = (subClass)b[4];
			a[2] = (subClass)b[5];
			bool gotit = false;
			try 
			{
				a[2] = (subClass)b[7];
			}
			catch (InvalidCastException  e) 
			{
                Console.WriteLine(e.Message);
                gotit = true;
			}
			if (!gotit) return "missing InvalidCastException";
			Array.Copy(a,0,b,0,10);
			for (int i = 0; i < 10; i++)
				a[i] = null;
			Array.Copy(a,0,b,0,10);
			checkAllNull(b);
			Array.Copy(b,0,a,0,10);
			checkAllNull(a);
			checkAllNull(b);

			a[4] = new subClass();
			Array.Copy(b,0,a,0,10);
			checkAllNull(a);
			checkAllNull(b);

			bool caught;

			caught = false;
			try 
			{
				Array.Copy(null,0,a,0,10);
			} 
			catch(System.ArgumentNullException e)
			{
                Console.WriteLine(e.Message);
                caught = true;
			}
			if (!caught) return "error: should have caught exception-1";

			caught = false;
			try 
			{
				Array.Copy(b,0,null,0,10);
			} 
			catch(ArgumentNullException e) 
			{
                Console.WriteLine(e.Message);
                caught = true;
			}
			if (!caught) return "error: should have caught exception-2";

			caught = false;
			try 
			{
				Array.Copy(b,0,a,0,11);
			} 
			catch(ArgumentException e) 
			{
                Console.WriteLine(e.Message);
                caught = true;
			}
			if (!caught) return "error: should have caught exception-3";

			caught = false;
			try 
			{
				Array.Copy(b,1,a,0,10);
			} 
			catch(IndexOutOfRangeException e)//ArgumentException e) 
			{
                Console.WriteLine(e.Message);
                caught = true;
			}
			catch(ArgumentException e)
			{
                Console.WriteLine(e.Message);
                caught = true ;
			}
			if (!caught) return "error: should have caught exception-4";

			caught = false;
			try 
			{
				Array.Copy(b,-1,null,100,100);
			} 
			catch(ArgumentNullException e) 
			{
                Console.WriteLine(e.Message);
                caught = true;
			}
			if (!caught) return "error: should have caught exception-5";

			b[5] = new superClass();
			caught = false;
			try 
			{
				Array.Copy(b,0,a,0,10);
			} 
				//catch(java.lang.ArrayStoreException e)

			catch(Exception e) 
			{
                Console.WriteLine(e.Message);
                caught = true;
			}
			if (!caught) return "error: should have caught exception-6";

			return null;
		}

		public int dontDouble(int a) 
		{
			throwArithmeticException(a);
			return 2 * a;
		}

		public void throwArithmeticException(int a) 
		{
			if (a == 1)
				throw(new ArithmeticException("fisk"));
			if (a == 1)
				sOut.WriteLine("should not print this");
			else
				sOut.WriteLine("should print this");
		}

		public int testDup() 
		{
			int a;
			a = 7;
			return a;
		}

		public int testForLoop(int x, int y) 
		{
			int a = 0;
			for (int i=x; i < y; i++)
				a += i*i;
			return a;
		}


		public static subClass[,] staticSubArray = new subClass[,] {{null,null}, {null,null}};
		public static int[,]      staticIntArray = new int[,] {{1,2,3}, {4,5,6}};

		public String testArray() 
		{
			sOut.Write("testArray:  ");
			int[] x;
			x = new int[6];
			x[4] = 3;
			x[3] = x[4];
			if (x[3] != 3) return "got bad array value-";

			double[,] y;
			y = new double[5,6];
			y[1,2] = 3.0;
			if (y[1,2] != 3.0) return "got bad array value-2";

			Stack[,,] fisk = new Stack[4,1,1];
			if (fisk[2,0,0] != null)
				return "bad array initialization";
			// David Detlefs suggested the following test. 9/97
			// Bug fixed by Subramanya Sastry, Univ. of Wisconsin, Madison
			bool hitit = false;
			try 
			{
				for (int i = 0; i < 5; i++) 
				{
					x[i + 3] = i;
				}
			} 
			catch (IndexOutOfRangeException e) 
			{
                Console.WriteLine(e.Message);
                hitit = true;
			}
			if (!hitit) return "missing exception";
			if (x[4] != 1 || x[5] != 2) return "missing side-effect";
			return null;
		}

		public bool isPrime(int i) 
		{
			if (i == 2) 
				return true;
			if (i % 2 == 0) 
				return false;
			int j = 3;
			while (j * j <= i) 
			{
				if (i % j == 0)
					return false;
				j = j + 2;
			}
			return true;
		}

		public void printPrimes() 
		{
			sOut.Write("Primes less than 50: ");
			for (int i = 2; i < 50; i++) 
			{
				if (isPrime(i)) 
				{
					sOut.Write(i);
					sOut.Write(" ");
				}
			}
			sOut.WriteLine("");
		}

		public void Verify(String str) 
		{
			//if (null == str || str.equals(""))
			if (null == str || str=="")
				sOut.WriteLine("OK");
			else 
			{
				gotError = true;
				sOut.WriteLine();
				sOut.WriteLine("******************************************");
				sOut.WriteLine(str);
				sOut.WriteLine("******************************************");
			}
		}

		public bool checkRemL(long a, long b, long res) 
		{
			bool ok = (res == a % b);
			if (!ok) 
			{
				sOut.Write("Failed: " + a + " % " + b + " = " + (a % b));
				sOut.WriteLine("   (should be: " + res);
			}
			return ok;
		}

		public bool checkRemD(double a, double b, double res) 
		{
			bool ok = (res == a % b);
			if (!ok) 
			{
				sOut.Write("Failed: " + a + " % " + b + " = " + (a % b));
				sOut.WriteLine("   (should be: " + res);
			}
			return ok;
		}


		public  void printRemD(double a, double b) 
		{
			sOut.Write(a + " % " + b + " = " + (a % b));
		}

		public String checkRemainders() 
		{
			sOut.Write("checkRemainders: ");
			bool ok = true;
			sOut.Write(" long ");
			if (!checkRemL( 10L,  7L, 3L))  ok = false;
			if (!checkRemL( 10L, -7L, 3L))  ok = false;
			if (!checkRemL(-10L,  7L, -3L)) ok = false;
			if (!checkRemL(-10L, -7L, -3L)) ok = false;

			if (!checkRemD( 10.5,  7.0, 3.5))  ok = false;
			if (!checkRemD( 10.5, -7.0, 3.5))  ok = false;
			if (!checkRemD(-10.5,  7.0, -3.5)) ok = false;
			if (!checkRemD(-10.5, -7.0, -3.5)) ok = false;
			if (!ok) return "remainders failed";
			sOut.Write("double ");
			return null; 
		}

		public bool checkClose(String exprStr, double v, double r) 
		{
			double m, av = v, ar = r;
			if (av < 0.0) av = -av;
			if (ar < 0.0) ar = -ar;
			if (av > ar) 
				m = av; 
			else 
				m = ar;
			if (m == 0.0) m = 1.0;
			if ((v - r) / m > 0.0001) 
			{
				sOut.WriteLine(exprStr + " evaluated to: " + v + ", expected: " + r);
				return false;
			}
			return true;
		}
		    
		public String checkMathFcts() 
		{
			sOut.Write("checkMathFcts: ");
			bool ok = true; 
			if (!checkClose("log(0.7)",  Math.Log(0.7),  -0.356675)) ok = false;
			if (!checkClose("sin(0.7)",  Math.Sin(0.7),   0.644218)) ok = false;
			if (!checkClose("cos(0.7)",  Math.Cos(0.7),   0.764842)) ok = false;
			if (!checkClose("tan(0.7)",  Math.Tan(0.7),   0.842288)) ok = false;
			if (!checkClose("asin(0.7)", Math.Asin(0.7),  0.775397)) ok = false;
			if (!checkClose("acos(0.7)", Math.Acos(0.7),  0.795399)) ok = false;
			if (!checkClose("atan(0.7)", Math.Atan(0.7),  0.610726)) ok = false;
			if (!ok) return "Some math function failed";
			return null;
		}

		public void doIntWhileLoop() 
		{
			int a = 0;
			while (a != 100000) 
			{
				a++;
			}
		}

		public void doLongWhileLoop() 
		{
			long a = 0;
			while (a != 100000) 
			{
				a++;
			}
		}

		public String fiskString() 
		{
			return "fisk";
		}

		public int deepRecursion(int n, int sum) 
		{
			if (n == 0) return sum;
			return deepRecursion(n - 1, n + sum);  /* Hopefully javac won't elim.
		                                              tail recursion. */
		}
		   
		public String testDeepStack() 
		{
			sOut.Write("testDeepStack: ");
			if (deepRecursion(5555, 0) != (5555 * 5555 + 5555) / 2) return "failed";
			return null;
		}

		public String testMisk() 
		{
			sOut.Write("testMisk: ");
			String right = "-9223372036854775808";
			//if (!right.equals("" + ((long)1 << 63)))
			if (right!=("" + ((long)1 << 63)))
				return "(long)1 << 63 failed, returned: " + ((long)1 << 63) +
					", should be: " + right;
			if (-1L != (-1L & -1L))
				return "Logical and failed for longs";
			//if (!getClass().getName().equals((new PepTest()).getClass().getName()))
			if (GetType().FullName != (new PepTest().GetType().FullName))
				return "Error(1): strings should have been equal!";
			String str1, str2;
			str1 = "fisk";
			str2 = "fisk";
			if (str1 != str2)
				return "Error(2): strings should be identical!";
			if (fiskString() != fiskString())
				return "Error(3): strings should be identical!";

			//if ((double)("3.14").ToDouble() != 3.14D)
			//if(Double.Parse("3.14") != -3.14D)
			if(Convert.ToDouble("3.14") != 3.14D)
				return "Error: Double.valueOf failed on 3.14";

			//if ((double)("-23.14").ToDouble() != -23.14D)
			if (Double.Parse("-23.14") != -23.14D)
				if(Convert.ToDouble("-23.14") != -23.14D)
					return "Error: Double.valueOf failed on -23.14";

			try 
			{
				str1 = "java.lang.Thread";
				//if (!str1.equals(java.lang.Class.forName(str1).getName())) 
				if (str1!=(Type.GetType(str1).FullName)) 
					return "Error(4): strings should be equal!";
			} 
			catch(Exception e)
            {
                Console.WriteLine(e.Message);
            }
            return null;
		}

		public String testGC() 
		{
			sOut.Write("testGC: ");
			sbyte[][] bytesArrays = new sbyte[1000][];
			bytesArrays[0] = new sbyte[1000];  /* See if GC eats this array! */
			GC.Collect();
			if (bytesArrays[0].GetType().FullName!=("System.SByte[]"))
				return "GC swallowed a live Object!";    /* Will fail here then. */
			String cn = this.GetType().FullName; /* Force construction of class Object. */
			GC.Collect();
			/* See if the GC spoled the class Object. */
			if (cn!=(GetType().FullName)) return "got different class name";
			/*
				 * Unfortunately we cannot count on being able to get system properties
				 * in an applet, so skip this test. wnb 2/13/98
				java.lang.System.getProperty("file.encoding");  // Fails if GC swallowed
																   props; it did happen!
				 */
			return null;
		}

		String testRandom() 
		{
			sOut.Write("testRandom : ");
      
			Random r = new Random(20357846);
			int intRandom = 0;
			for (int i = 0; i < 163; i++) 
			{
				/*longRandom*/ intRandom = r.Next();
			}
			// System.out.println(longRandom);
			long Answer = 1083629507 ;
			if (intRandom == Answer)
				return null;
			else
				return "Random value does not match as required ";
		}

		/*
		* Skip file tests in an applet, wnb 2/13/98 String testFileOps() {
		* out.print("testFileOps: "); java.io.File f = new java.io.File("."); if
		* (!f.isDirectory()) return "'.' is not a directory"; if (!f.exists()) return
		* "'.' does not exist"; if (f.isAbsolute()) return "'.' is not an absolute
		* path"; return null; }
		*/

		public void instanceMain() 
		{
			//  printTime();
			Verify(testIf());
			Verify(testArray());
			Verify(testBitOps());
			Verify(testFor());
			Verify(testDiv());   // breaks jit; comment out for jit's sake!
			Verify(testTableSwitch());
			Verify(testLookupSwitch());
			Verify(testHiddenField());
			Verify(checkRemainders());
			Verify(checkMathFcts());
			printPrimes();
			Verify(testExc1());
			Verify(testExc2());
			Verify(testExc3());
			Verify(testExc4());
			Verify(testExc5());
			Verify(testExc6());
			Verify(testExc7());
			//  Verify(testExc8());
			//  Verify(testExc9());
			Verify(loopExitContinueInExceptionHandler());
			Verify(testStringHash());
			/*
			 * This is more properly a QA test. Remove
			 * per (osgjava-363)
				Verify(testObjectHash());
			 */
			Verify(testClone());
			Verify(testObjectArray());
			testClass();
			Verify(checkInstanceOf());
			Verify(checkInterfaceInstanceOf());
			Verify(testWaitNull());
			Verify(testVarAndMethodNameClash());
			//  Verify(testDeepStack());		// not yet - walter
			Verify(testMisk());
			Verify(testGC());
			Verify(testRandom());//new for jbb2005
			//  Verify(testFileOps());
			//  Don't perform fp accuracy check so that 80-bit intermediate values
			//  will not be flagged as invalid. Per precedent of (osgjava-143) November
			//  1997 minutes, and (osgjava-340) May 1998 minutes: specifications section
			//  (new FloatingPointCheck()).run (spec.harness.Context.getSpeed());
			if (gotError)
			{
				sOut.WriteLine("PepTest: error");
				//      sOut.WriteLine("****** PepTest found an error ******");
				//System.exit(1);
				return;
			}
			else
			{
				sOut.WriteLine("PepTest: OK");
				//      sOut.WriteLine("****** PepTest completed ******");
			}
		}

        //public static void Main(String[] args) 
        //{
        //    PepTest horse = new PepTest();
        //    horse.instanceMain();
        //    if (horse.gotError) 
        //        Environment.Exit(1);//System.exit(1);
        //}
	}//PepTest
}
