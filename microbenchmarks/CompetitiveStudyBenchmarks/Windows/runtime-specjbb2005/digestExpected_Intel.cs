/*
 *
 * Copyright (c) 2000-2005 Standard Performance Evaluation Corporation (SPEC)
 *               All rights reserved.
 * Copyright (c) 2000-2005 Hewlett-Packard        All rights reserved.
 * This source code is provided as is, without any express or implied warranty.
 *
 */
using System;

namespace Specjbb2005.src.spec.jbb.Validity
{
	/// <summary>
	/// Summary description for digestExpected.
	/// </summary>
	public class digestExpected
	{
		// This goes right after each class/interface statement
		//  static readonly String COPYRIGHT = "SPECjbb2005,"
		//  + "Copyright (c) 2005 Standard Performance Evaluation Corporation (SPEC),"
		//  + "All rights reserved,"
		//  + "Copyright (c) 2005 Hewlett-Packard,"
		//  + "All rights reserved,"
		//  + "Licensed Materials - Property of SPEC";

		//This is for JBB jar crap. Taken from SPECJbb2000. 
		byte[] a = { 113, 67, 38, 199, 224, 238, 30, 198, 117, 93 };
/*
		sbyte[]                a       = 
		{
			-120, 38, -16, -122, 8, -70, -108, 33, -112, 125
		};
*/
		public digestExpected() 
		{

		}

		public byte[] getArray() 
		{
			return a;
		}
	}
}
