/*
 * 
 * Copyright (c) 2000-2005 Standard Performance Evaluation Corporation (SPEC) All
 * rights reserved. Copyright (c) 1996-2005 IBM Corporation, Inc. All rights
 * reserved.
 */
using System;
using System.Collections;
using Specjbb2005.src.spec.jbb.infra.Util;
using System.Runtime.CompilerServices;

namespace Specjbb2005.src.spec.jbb
{
	/// <summary>
	/// Summary description for Warehouse.
	/// </summary>
	public class Warehouse
	{
		// This goes right after each class/interface statement
		//  static readonly String    COPYRIGHT = "SPECjbb2005,"
		//  + "Copyright (c) 2005 Standard Performance Evaluation Corporation (SPEC),"
		//  + "All rights reserved,"
		//  + "(C) Copyright IBM Corp., 1996 - 2005"
		//  + "All rights reserved,"
		//  + "Licensed Materials - Property of SPEC";

		// required fields
		private String         name;

		private Address        address;

		private Decimal        taxRate;

		private Decimal        ytd;

		private short          warehouseId;

		private Company        company;

		private JBBDataStorage stockTable;

		private JBBDataStorage itemTable;

		private Object[]       districts;

		private int            distCount;

		private Random         per_wh_r;

		private JBBDataStorage historyTable;

		private long           historyCount;

		private long           oldestHistory;

		public Warehouse(Company inCompany, JBBDataStorage inItemTable) 
		{
			company = inCompany;
			districts = new Object[20];
			stockTable = Infrastructure.createStorage();
			itemTable = inItemTable;
			address = new Address();
			historyTable = Infrastructure.createSortedStorage();
			historyCount = 0;
			oldestHistory = 0;
			ytd = Decimal.Zero ;//BigDecimal.valueOf(0, 2);
			loadStockTable();
		}

		public Warehouse() 
		{

		}

		public void initWarehouse(Company inCompany, JBBDataStorage inItemTable,
			short warehouseId) 
		{
			per_wh_r = JBButil.derived_random_init(warehouseId);
			JBButil.register_warehouse_Random_stream(warehouseId, per_wh_r);
			this.warehouseId = warehouseId;
			ytd = Decimal.Zero;//BigDecimal.valueOf(0, 2);
			company = inCompany;
			districts = new Object[20];
			stockTable = Infrastructure.createStorage();
			itemTable = inItemTable;
			address = new Address();
			historyTable = Infrastructure.createSortedStorage();
			historyCount = 0;
			oldestHistory = 0;
			ytd = Decimal.Zero;//BigDecimal.valueOf(0, 2);
			loadStockTable();
		}

		public bool validDistrict(sbyte inDistrictId) 
		{
			District currentDistrict;
			int i;
			for (i = 0; i < distCount; i++) 
			{
				currentDistrict = (District) districts[i];
				if (currentDistrict.getId() == inDistrictId)
					return true;
			}
			return false; // not found
		}

		public short getId() 
		{
			return warehouseId;
		}

		public Decimal getTaxRate() 
		{
			return taxRate;
		}

		public Address getAddress() 
		{
			return address;
		}

		public District getDistrictPtr(sbyte inDistrictId, bool lockFlag) 
		{
			District result = null;
			if (inDistrictId > 0) 
			{
				inDistrictId--;
				if (inDistrictId < distCount)
					result = (District) districts[inDistrictId];
			}
			return result;
		}

		public Stock retrieveStock(int inItemId) 
		{
			return (Stock) stockTable.get(inItemId);
		}

		public Item retrieveItem(int inItemId) 
		{
			return (Item) itemTable.get(inItemId);
		}

        private readonly object _syncRoot = new Object(); // CORECLR

        // CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public void receivePayment(Decimal paymentAmount)
        {
            lock (_syncRoot)
            {
                ytd = Decimal.Add(ytd, paymentAmount);//ytd.add(paymentAmount);
            }
        }
		public District getDistrict(int distId) 
		{
			District result = null;
			if (distId > 0) 
			{
				distId--;
				if (distId < distCount)
					result = (District) districts[distId];
			}
			return result;
		}

		public int getDistrictCount() 
		{
			return distCount;
		}

        // CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public void trimOrdersForSteadyState()
        {
            lock (_syncRoot)
            {
                int initialOrders = company.getInitialOrders();
                int initialNewOrders = company.getInitialNewOrders();
                trimOrdersForSteadyState(initialOrders, initialNewOrders);
            }
        }
        // CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public void trimOrdersForSteadyState(int initialOrders, int initialNewOrders)
        {
            lock (_syncRoot)
            {
                int distCount = this.getDistrictCount();
                int distId;
                // iterate over the district list of the given warehouse
                District currentDistrict;
                for (distId = 1; distId <= distCount; distId++)
                {
                    currentDistrict = this.getDistrict(distId);
                    if (Transaction.steadyStateMem)
                    {
                        currentDistrict.removeOldNewOrders((initialOrders - initialNewOrders));
                        currentDistrict.removeOldOrders(initialOrders);
                    }
                }
            }
        }
        // CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public void setUsingRandom(short inWarehouseId)
        {
            lock (_syncRoot)
            {
                int i;
                warehouseId = inWarehouseId; // set what we get as input into the
                                             // object
                name = new String(JBButil.create_random_a_string(6, 10, per_wh_r));
                address.setUsingRandom(per_wh_r); // address
                float temp = JBButil.create_random_float_val_return(0.0f, 0.2000f,
                    0.0001f, per_wh_r);
                taxRate = Math.Round(new Decimal(temp), 4);//Convert.ToDecimal(temp);//new BigDecimal(temp).setScale(4, BigDecimal.ROUND_HALF_UP);
                ytd = new Decimal(30000000);//,0,0,false,2);//BigDecimal.valueOf(30000000, 2);
                distCount = company.getMaxDistrictsPerWarehouse();
                for (i = 0; i < distCount; ++i)
                {
                    sbyte newDistrictId = (sbyte)(i + 1);
                    District newDistrict = new District(warehouseId, newDistrictId);
                    newDistrict.setUsingRandom();
                    districts[i] = newDistrict;
                }
            }
        }

		public String getName() 
		{
            return name; // CORECLR  String.Copy(name);
		}

		public void loadStockTable() 
		{
			//Iterator itemIter = itemTable.elements();
			IEnumerator itemIter = itemTable.elements();
			Item currItem;
			Stock newStock;
			int i = 0;
                  while (itemIter.MoveNext()) 
			{
				currItem = (Item) itemIter.Current;//.next();
				i++;
				newStock = new Stock(company, currItem.getId(), warehouseId);
				stockTable.put(newStock.getId(), newStock);
			}
		}

		private Object updateHistorySynch = new Object();

		public void updateHistory(History inHistory) 
		{
                        //09-04-07 Li: need lock histrotyTable before update because in CustomerReportTransaction.cs
                        //it used enumeration of this table, it will throw out invalidoperationException
                        //duraing enumeration if the underneath collection has changed, also get rid of
                        //updateHistorySynch lock since we already have lock here 
			lock (historyTable) 
			{
				historyCount++;
                                historyTable.put(historyCount, inHistory);
			}
		}

		private Object removeOldestHistorySynch = new Object();

		public void removeOldestHistory() 
		{
                        //09-04-07 Li: need lock histrotyTable before update because in CustomerReportTransaction.cs
                        //it used enumeration of this table, it will throw out invalidoperationException
                        //duraing enumeration if the underneath collection has changed, also get rid of
                        //removeHistorySynch lock since we already have lock here 
			lock (historyTable) 
			{
				oldestHistory++;
                                historyTable.remove(oldestHistory);
			}
		}

        // CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public void loadHistoryTable()
        {
            lock (_syncRoot)
            {
                String historyData;
                // go through all of the districts for each warehouse
                for (sbyte districtId = 1; districtId <= company
                    .getMaxDistrictsPerWarehouse(); ++districtId)
                {
                    // go through all of the customers for each district
                    for (short customerId = 1; customerId <= company
                        .getMaxCustomersPerDistrict(); ++customerId)
                    {
                        long uniqueCustomerNumber = company.buildUniqueCustomerKey(
                            warehouseId, districtId, customerId);
                        // get customer
                        Customer customerPtr = company.getCustomer(
                            uniqueCustomerNumber, false);
                        Decimal amount = new Decimal(1000, 0, 0, false, 2);//BigDecimal.valueOf(1000, 2);
                        historyData = new String(JBButil.create_random_a_string(12, 25, warehouseId));//new String(JBButil.create_random_a_string(12, 25,warehouseId));
                        DateTime creation_time = DateTime.Now;//new Date();
                        History newHistory = new History(customerId, customerPtr
                            .getDistrictId(), customerPtr.getWarehouseId(),
                            districtId, warehouseId, creation_time, amount,
                            historyData);
                        updateHistory(newHistory);
                    }
                }
            }
        }
		// CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
		public JBBDataStorage getHistoryTable() 
		{
			return historyTable;
		}


	}
}
