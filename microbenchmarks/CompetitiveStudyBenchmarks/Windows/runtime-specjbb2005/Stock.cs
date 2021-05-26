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
using System.Runtime.CompilerServices;

namespace Specjbb2005.src.spec.jbb
{
	/// <summary>
	/// Summary description for Stock.
	/// </summary>
	public class Stock
	{
		// This goes right after each class/interface statement
		//  static readonly String COPYRIGHT = "SPECjbb2005,"
		//  + "Copyright (c) 2005 Standard Performance Evaluation Corporation (SPEC),"
		//  + "All rights reserved,"
		//  + "(C) Copyright IBM Corp., 1996 - 2005"
		//  + "All rights reserved,"
		//  + "Licensed Materials - Property of SPEC";

		// misc:
		private Company     company;

		// required fields
		private String[]      district_text;

		private String      data;

		private int         id;

		private int         quantity;

		//private int       ytd; 
        private Decimal     ytd;

		private short       orderCount;

		private short       remoteCount;

		private short       warehouseId;

		public Stock(Company inCompany, int itemId, short wId) 
		{
			this.initStock(inCompany, itemId, wId);
		}

		public void initStock(Company inCompany, int itemId, short wId) 
		{
			int district;
			short maxDistricts;
			int hit;
			quantity = (int) JBButil.random(10, 100, wId);
			ytd = 0 ;//BigInteger.ZERO;
			orderCount = 0;
			remoteCount = 0;
			hit = 0;
			data = new String(JBButil.create_a_string_with_original(26, 50, 10f,hit, wId));//new String(JBButil.create_a_string_with_original(26, 50, 10f,hit, wId));
			company = inCompany;
			id = itemId;
			warehouseId = wId;
			maxDistricts = company.getMaxDistrictsPerWarehouse();
			district_text = new String[maxDistricts];
			for (district = 0; district < maxDistricts; district++) 
			{
				district_text[district] = new String(JBButil
					.create_random_a_string(25, 25, warehouseId));
			}
		}
        private readonly object _syncRoot = new Object(); // CORECLR

        // CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public int getQuantity()
        {
            lock (_syncRoot)
            {
                int temp = quantity;
                return temp;
            }
        }
        // CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public void changeQuantity(int delta)
        {
            lock (_syncRoot)
            {
                quantity += delta;
            }
        }
        // CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public void incrementOrderCount()
        {
            lock (_syncRoot)
            {
                ++orderCount;
            }
        }
        // CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public void incrementRemoteCount()
        {
            lock (_syncRoot)
            {
                ++remoteCount;
            }
        }
        // CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public int getId()
        {
            lock (_syncRoot)
            {
                int temp = id;
                return temp;
            }
        }
        // CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public String getDistrictInfo(sbyte districtId)
        {
            lock (_syncRoot)
            {
                return district_text[districtId - 1];
            }
        }
        // CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public String getData()
        {
            lock (_syncRoot)
            {
                // CORECLR String temp = String.Copy(data);
                return data;
            }
        }
        // CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public void incrementYTD(short ol_quantity)
        {
            lock (_syncRoot)
            {
                //ytd = ytd + ol_quantity ; //ytd.add(BigInteger.valueOf(ol_quantity));
                ytd = Decimal.Add(ytd, ol_quantity);
            }
        }
	}
}
