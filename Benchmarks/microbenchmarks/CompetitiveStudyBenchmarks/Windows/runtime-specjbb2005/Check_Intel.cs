/*
 *
 * Copyright (c) 2000-2005 Standard Performance Evaluation Corporation (SPEC) All
 * rights reserved. 
 */
/*
 * Complain about Java errors that ought to be caught as exceptions by the JVM.
 * *NOT* an exhaustive conformance test. This is just intended to catch some
 * common errors and omissions for the convenience of the benchmarker in
 * avoiding some run rule mistakes. This needs to be expanded to test more.
 *
 * The timing of this "benchmark" is ignored in metric calculation. It is here
 * only in order to pass or fail output verification.
 */using System;

namespace Specjbb2005.src.spec.jbb.Validity
{
	/// <summary>
	/// Summary description for Check.
	/// </summary>
	public class Check
	{
		// This goes right after each class/interface statement
		//  static readonly String COPYRIGHT = "SPECjbb2005,"
		//  + "Copyright (c) 2005 Standard Performance Evaluation Corporation (SPEC),"
		//  + "All rights reserved,"
		//  + "Licensed Materials - Property of SPEC";

		public static bool doCheck() 
		{
			Console.WriteLine("\nChecking CLR\n");
			bool caughtIndex = false;
			bool gotToFinally = false;
			bool error = false;
			try 
			{
				int[] a = new int[10];
				for (int i = 0; i <= 10; i++)
					a[i] = i;
				Console.WriteLine("Error: array bounds not checked");
				error = true;
			}
			catch (IndexOutOfRangeException e) 
			{
                Console.WriteLine(e.Message);
                caughtIndex = true;
			}
			finally 
			{
				gotToFinally = true;
			}
			if (!caughtIndex) 
			{
				Console.WriteLine("1st bounds test error:\tindex exception not received");
				error = true;
			}
			if (!gotToFinally) 
			{
				Console.WriteLine("1st bounds test error:\tfinally clause not executed");
				error = true;
			}
			if (caughtIndex && gotToFinally)
				Console.WriteLine("1st bounds test:\tOK");
			if (checkSubclassing())
				error = true;
			if (checkXMLErrorChecking())
				error = true;
			LoopBounds mule = new LoopBounds();
			LoopBounds.run();
			if (LoopBounds.gotError) 
			{
				Console.WriteLine("2nd bounds test:\tfailed");
				error = true;
			}
			else 
			{
				Console.WriteLine("2nd bounds test:\tOK");
			}
			PepTest horse = new PepTest();
			horse.instanceMain();
			if (horse.gotError)
				error = true;
			if (error)
				Console.WriteLine("\nINVALID: CLR Check detected error(s)");
			else
				Console.WriteLine("\nCLR Check OK");
			return error;
		}

		private static bool checkSubclassing() 
		{
			bool error = false;
			Super sup = new Super(3);
			Sub sub = new Sub(3);
			Console.WriteLine(sup.getName() + ": " + sup.ToString());
			Console.WriteLine(sub.getName() + ": " + sub.ToString());
			if (!sup.ToString().Equals(
				"Class Super, public=34, protected=33, private=32"))
				error = true;
			if (!sub.ToString().Equals(
				"Class Super, public=804, protected=803, private=802"))
				error = true;
			Console.WriteLine("Super: prot=" + sup.getProtected() + ", priv="
					   + sup.getPrivate());
			Console.WriteLine("Sub:  prot=" + sub.getProtected() + ", priv="
					   + sub.getPrivate());
			if (sup.getProtected() != 33 || sup.getPrivate() != 32)
				error = true;
			if (sub.getProtected() != 111 || sub.getPrivate() != 105)
				error = true;
			Console.WriteLine("Subclass test " + (error ? "error" : "OK"));
			return error;
		}

		//TODO:Check.this.Later. Do we need this for CLR?
		private static bool checkXMLErrorChecking() 
		{
			bool error = false;
			/*
			// initialize document
			bool error = false;
			Document document;
			DocumentBuilder builder;
			DocumentBuilderFactory factory = DocumentBuilderFactory.newInstance();
			try 
			{
				builder = factory.newDocumentBuilder();
				document = builder.newDocument();
				bool result = false;
				result = document.getStrictErrorChecking(); // Comment this line out
				// if compiling with a
				// 1.4 JDK.
				// strictErrorChecking must be true.
				if (result)
					error = false;
				else
					error = true;
				Console.WriteLine("XML StrictErrorChecking test: "+ (error ? "error" : "OK"));
			}
			catch (ParserConfigurationException pce) 
			{
				// Parser with specified options can't be built
				pce.printStackTrace();
			}
			*/
			return error;
		}
		

	}//Check
}

	
