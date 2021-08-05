/*
 *
 * Copyright (c) 2000 Standard Performance Evaluation Corporation (SPEC)
 *               All rights reserved.
 * This source code is provided as is, without any express or implied warranty.
 *
 */
using System;

namespace Specjbb2005.src.spec.jbb.Validity
{
	/// <summary>
	/// Summary description for Sub.
	/// </summary>
	public class Sub : Super
	{
			// This goes right after each class/interface statement

//  			static readonly String COPYRIGHT =
//  
//  				"SPECjbb2005,"+
//  
//  				"Copyright (c) 2005 Standard Performance Evaluation Corporation (SPEC),"+
//  
//  				"All rights reserved,"+
//  
//  				"Licensed Materials - Property of SPEC";

			///////////////////////////////////////
			//class variable field declarations
			///////////////////////////////////////

			private static string name = "Sub";
			private static int psi = publicStatic + 7;

			///////////////////////////////////////
			//instance variable field declarations
			///////////////////////////////////////

			private int priv = 5;
			//protected int prot = 11;
			//public int pub = 13;
			new protected int prot = 11;
			new public int pub = 13;

			///////////////////////////////////////
			//constructor declarations
			///////////////////////////////////////

			public Sub (int black) : base (black + 77)
			{
				//super (black + 77);
				pub += black * 2;
			}

			///////////////////////////////////////
			//class method declarations
			///////////////////////////////////////

			///////////////////////////////////////
			//instance method declarations
			///////////////////////////////////////

			new public string getName()
			{
				return name;
			}

			new public int getPrivate()
			{
				return priv + 100;
			}

			new public int getProtected()
			{
				return prot + 100;
			}

	}
}
