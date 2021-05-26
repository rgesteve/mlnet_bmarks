/*
 * 
 * Copyright (c) 2000-2005 Standard Performance Evaluation Corporation (SPEC) All
 * rights reserved. Copyright (c) 1996-2005 IBM Corporation, Inc. All rights
 * reserved.
 * 
 */
using System;
using System.Runtime.CompilerServices;

namespace Specjbb2005.src.spec.jbb
{
	/// <summary>
	/// Summary description for Item.
	/// </summary>
	public class Item
	{
		// This goes right after each class/interface statement
		//  internal static readonly String COPYRIGHT = "SPECjbb2005,"
		//  + "Copyright (c) 2000-2005 Standard Performance Evaluation Corporation (SPEC),"
		//  + "All rights reserved,"
		//  + "(C) Copyright IBM Corp., 1996 - 2005"
		//  + "All rights reserved,"
		//  + "Licensed Materials - Property of SPEC";

		private String      name;

		private String      brandInformation;

		private Decimal     price;

		private int         id;

		public Item() 
		{
		}

		public int getId() 
		{
			return id;
		}

		public String getName() 
		{
			return name;
		}

		public String getBrandInfo() 
		{
			return brandInformation;
		}

		public Decimal getPrice() 
		{
			return price;
		}

        private readonly object _syncRoot = new Object(); // CORECLR

        // CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public void setUsingRandom(int inId)
        {
            lock (_syncRoot)
            {
                id = inId;
                name = new String(JBButil.create_random_a_string(14, 24));//new String(JBButil.create_random_a_string(14, 24));
                float temp = JBButil.create_random_float_val_return(1.00f, 100.00f, .01f);
                price = System.Math.Round(new System.Decimal(temp), 2);//Convert.ToDecimal(temp);//new BigDecimal(temp).setScale(2, BigDecimal.ROUND_HALF_UP);
                                                                       //doesn;t matter if we pass 0 as hit as it is not used in that function anyway
                brandInformation = new String(JBButil.create_a_string_with_original(26, 50, 10, 0));//new String(JBButil.create_a_string_with_original(26,50, 10, /* hit */null));
            }
        }
	}
}
