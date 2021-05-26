/*
 * 
 * Copyright (c) 2000-2005 Standard Performance Evaluation Corporation (SPEC) All
 * rights reserved. Copyright (c) 2000-2005 Hewlett-Packard All rights reserved.
 * 
 * This source code is provided as is, without any express or implied warranty.
 *  
 */
using System;
using System.IO;
using System.Collections;
using System.Runtime.CompilerServices;
// CORECLR using System.Security.Cryptography;

namespace Specjbb2005.src.spec.jbb.Validity
{
	/// <summary>
	/// Summary description for digest.
	/// </summary>
	public class digest
	{
		// This goes right after each class/interface statement
		//  static readonly String COPYRIGHT = "SPECjbb2005,"
		//  + "Copyright (c) 2005 Standard Performance Evaluation Corporation (SPEC),"
		//  + "All rights reserved,"
		//  + "Copyright (c) 2005 Hewlett-Packard,"
		//  + "All rights reserved,"
		//  + "Licensed Materials - Property of SPEC";

		bool             debug;

		public digest() 
		{
			debug = false;
		}

		public bool crunch_jar(string name)
		{
			bool correct = true;
			//String path = System.getProperty("java.class.path");
			//This is not relevant for C#
			//string path = System.getProperty("java.class.path");
			String path = Environment.GetEnvironmentVariable("java.class.path");

			// find jbb.jar
			//int index_jar = path.indexOf("jbb.jar");
			
			//Should be in a try catch block
			//ArgumentNullException & ArgumentOutOfRangeException 
			String jar_name = "jbb.jar";

			try 
			{
				int index_jar = path.IndexOf ("jbb.jar");
				// check that either it's at char 1
				if (index_jar < 0)
				{
					correct = false;
					Console.WriteLine("jbb.jar not in CLASSPATH");
					return false;
				}

				if (index_jar > 0)
				{
					// 	or there's a File.separator before it by 1
					//if (path.charAt(index_jar - 1) != File.separatorChar)
					if (path[(index_jar - 1)]!= Path.DirectorySeparatorChar )
					{
						correct = false;
						return false;
					} 
					// AND no path.separator before it
					// int index_separator = path.indexOf(File.pathSeparatorChar);
					int index_separator = path.IndexOf (Path.PathSeparator);
					if (index_separator < index_jar)
					{
						Console.WriteLine("fails validation because something is before jbb.jar in CLASSPATH");
						correct = false;
						return false;
					}
					// fill in full name 0 - end of ".jar"
					//jar_name = path.substring(0, index_jar + 7);	
					jar_name = path.Substring(0, index_jar + 7);
				}	
			}
			catch(NullReferenceException e)
			{
				Console.WriteLine("NullReferenceException error " + e.Message);
			}
			catch (ArgumentNullException e)
			{
				Console.WriteLine("ArgumentNullException error " + e.Message);
			}
			catch (ArgumentOutOfRangeException e)
			{
				Console.WriteLine("ArgumentOutOfRangeException error " + e.Message);
            }


			// open that File
			try 
			{
				// Porting note:  this block of code produces the same result as
				// the Java version but it's implementation is pretty different. -bw
				FileInfo the_jar = new FileInfo(jar_name);
				Stream fileStream = the_jar.OpenRead();
							
				digestExpected e = new digestExpected();
				byte[] expected = e.getArray();

                //SHA1 md = new SHA1_CSP(); // this is one implementation of the abstract class SHA1;
                // CORECLR SHA1 md = new SHA1CryptoServiceProvider();
                //MD5	md = new MD5CryptoServiceProvider() ;
                /*
				int count = (int) (the_jar.Length);
				byte[] oneByteArray = new byte[1];
				for (int i = 0; i < count; i++) 
				{
					// porting note: this may seem screwy but I didn't want to introduce
					// a performance improvement in the C# version by reading in bigger 
					// blocks from the file.  I'm pretty sure the Java version also 
					// reads one byte at a time.  -bw
					oneByteArray[0] = (byte)fileStream.ReadByte();
					md.Write(oneByteArray); 
				}
				
				md.CloseStream();	
		           
				//byte a[] = md.digest();
				byte[] a = md.Hash;
				for (int i = 0; i < 10; i++) 
				{
					if (debug) Console.WriteLine(" , {0}" , a[i]);
					if (a[i] != expected[i])
						correct = false;
				}
				*///Bryan's code block ends
                  //My code block starts

                // CORECLR byte[] a = md.ComputeHash(fileStream) ;
                // CORECLR for (int i = 0; i < 10; i++) 
                // CORECLR {
                // CORECLR if (debug) Console.WriteLine(" , {0}" , a[i]);
                // CORECLR if (a[i] != expected[i])
                // CORECLR correct = false;
                // CORECLR }
                // CORECLR fileStream.Close() ;
                //My code block ends
                Console.WriteLine ("CORECLR: Commented sha1 jar validity is {0}", correct);

				return correct;
			}
			catch (Exception e) 
			{
				Console.WriteLine("digest:  caught exception {0}", e);
			}
			return false;
		} 
	} 

}
