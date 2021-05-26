/*
 *
 * Copyright (c) 2000-2005 Standard Performance Evaluation Corporation (SPEC)
 *               All rights reserved.
 * This source code is provided as is, without any express or implied warranty.
 *
 */
/*
 * Check whether the JVM makes unwarranted assumptions about non-final
 * methods. This test case is derived from a hot spot in DeltaBlue
 * where it seems tempting to make the loop bounds constant, but
 * you cannot be certain that the class is not subclassed, etc.
 *
 * Walter Bays
 */
using System;
using System.Collections;

namespace Specjbb2005.src.spec.jbb.Validity
{
	/// <summary>
	/// Summary description for LoopBounds.
	/// </summary>
	/// 
	public class LoopBounds
	{
		// This goes right after each class/interface statement
		static readonly String   COPYRIGHT = "SPECjbb2005,"
		+ "Copyright (c) 2000-2005 Standard Performance Evaluation Corporation (SPEC),"
		+ "All rights reserved,"
		+ "Licensed Materials - Property of SPEC";

		protected System.Collections.ArrayList v;
		public static bool gotError	= false;

		public LoopBounds()	
		{
			//v = new ArrayList();
                  v = ArrayList.Synchronized(new ArrayList());  
			int	f0 = 0;
			int	f1 = 1;
			for	(int i=0; i	< 20; i++)
			{
				//v.addElement (new Integer(f1));
				v.Add (f1);
				int	f =	f0 + f1;
				f0 = f1;
				f1 = f;
			}
		}

		public virtual int size() 
		{ 
			//return v.size(); 
			return v.Count; 
		}

		public int constraintAt(int	index) 
		{
			//Object o = v.elementAt(index);
			Object o = v[index];

			//if (o	is Integer)
			if (o is int)
				return ((int)o);
			else
				return 666;
		}

		public void	execute()
		{
			for	(int i=	0; i < size(); ++i)	
			{
				//System.out.WriteLine ("v.size()=" +	v.size() + " size()=" +	size());
				Console.Write ("{0}	" ,	constraintAt(i));
			}
			Console.WriteLine();
		}

		
		//  public static void Main	(string[] args)
		//  {
		//  	run();
		//  }

		// John Benninghoff 2002-Jun-11  This is a static method in Java
		public static void run()
		{
			(new LoopBounds()).execute();
			string name	= "Specjbb2005.src.spec.jbb.Validity.LoopBounds2";
			try
			{
				//Class	c =	Class.forName (name);
				Type c = Type.GetType (name);

				//Object o = c.newInstance();
				Object o = Activator.CreateInstance(c);
				if (!(o	is LoopBounds))
				{
					//Console.WriteLine	(name +	" is not a LoopBounds\n");
					Console.WriteLine ("{0}	is not a LoopBounds\n",	name);
					gotError = true;
					return;
				}
				((LoopBounds)o).execute();
			}
			catch (Exception e)
			{
				Console.WriteLine ("Error {0}" , e);
				gotError = true;
			}
		}

	}//end LoopBounds

	internal class LoopBounds2 :	LoopBounds
	{

		// This	goes right after each class/interface statement
		static readonly String COPYRIGHT =
			"SPECjbb2000,"+
			"Copyright (c) 2000 Standard Performance Evaluation Corporation (SPEC),"+
			"All rights reserved,"+
			"Licensed	Materials -	Property of	SPEC";


		private	int	n =	0;

		public override int size() 
		{
			if (n >	4 && n%2 ==	1)
				//v.insertElementAt (new Double (1.0 / n),6);
				v.Insert ( 6, (1.0 / n));

			//return v.size()	- n++;
			return v.Count	- n++;
		}
	}//end LoopBounds2
}

