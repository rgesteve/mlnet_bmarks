/*
 * 
 * Copyright (c) 2000-2005 Standard Performance Evaluation Corporation (SPEC) All
 * rights reserved. Copyright (c) 1996-2005 IBM Corporation, Inc. All rights
 * reserved.
 * 
 */
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Collections;

namespace Specjbb2005.src.spec.jbb
{
    /// <summary>
    /// Summary description for District.
    /// </summary>
    public class District
    {

        // This goes right after each class/interface statement
        //  internal static readonly String COPYRIGHT = "SPECjbb2005,"
        //  + "Copyright (c) 2000-2005 Standard Performance Evaluation Corporation (SPEC),"
        //  + "All rights reserved,"
        //  + "(C) Copyright IBM Corp., 1996 - 2005"
        //  + "All rights reserved,"
        //  + "Licensed Materials - Property of SPEC";

        private JBBSortedStorage orderTable;

        private JBBSortedStorage newOrderTable;

        // required
        private String name;

        private Address address;

        private Decimal taxRate;

        private Decimal ytd;

        private int nextOrder;

        private sbyte districtId;

        private short warehouseId;

        private int oldestOrder;

        private NewOrder preVal;
        private readonly object _syncRoot = new Object(); // CORECLR

        public District(short inWarehouseId, sbyte inDistrictId)
        {
            districtId = inDistrictId;
            warehouseId = inWarehouseId;
            address = new Address();
            nextOrder = 1;
            orderTable = Infrastructure.createSortedStorage();
            newOrderTable = Infrastructure.createSortedStorage();
        }

        public int getId()
        {
            return districtId;
        }

        public Decimal getTaxRate()
        {
            return taxRate;
        }

        public Address getAddress()
        {
            return address;
        }

        // CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public int lastOrderId()
        {
            lock (_syncRoot)
            {
                int temp = nextOrder - 1;
                return temp;
            }
        }

        // CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public int nextOrderId()
        {
            lock (_syncRoot)
            {
                int temp = nextOrder++;
                return temp;
            }
        }
        // CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public void display()
        {
            lock (_syncRoot)
            {
                Console.WriteLine("District display ****************************");
                Console.WriteLine("districtId is ======== " + districtId);
                Console.WriteLine("warehouseID == " + warehouseId);
                Console.WriteLine("name is   ==== " + name);
                address.display();
                Console.WriteLine("*********************************************");
            }
        }
        // CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public void receivePayment(Decimal paymentAmount)
        {
            lock (_syncRoot)
            {
                ytd = Decimal.Add(ytd, paymentAmount);//ytd.add(paymentAmount);
            }
        }
        //todo:ADD Decimal.rOUND?
        // CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public void setUsingRandom()
        {
            lock (_syncRoot)
            {
                name = new String(JBButil.create_random_a_string(6, 10, warehouseId));
                address.setUsingRandom(warehouseId); // address
                float temp = JBButil.create_random_float_val_return(0.0f, 0.2000f, 0.0001f, warehouseId);
                //taxRate = Convert.ToDecimal(temp) ;//new BigDecimal(temp).setScale(4, BigDecimal.ROUND_HALF_UP); // tax
                // CORECLR taxRate = Decimal.Round(new System.Decimal(temp), 4); // tax
                taxRate = Math.Round(new System.Decimal(temp), 4); // tax

                // rate
                //ytd = new Decimal(3000000,0,0,false,2); //Convert.ToDecimal(3000000L);//BigDecimal.valueOf(3000000L, 2);
                ytd = new System.Decimal(3000000L); //Milind after looking @ JCLA
            }
        }
        //// CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public void addOrder(Order anOrder)
        {
            lock (_syncRoot)
            {
                //09-04-07 Li: need lock orderTable because in CustomerReporTransaction.cs 
                //it is the underneath collecion of enumeration. We lock this which doing 
                //changed on the collection so that the enumeration will be happy.
                lock (orderTable)
                {
                    orderTable.put(anOrder.getId(), anOrder);
                }
            }
        }
        // CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public Order retrieveOrder(Order order)
        {
            lock (_syncRoot)
            {
                return (Order)orderTable.get(order.getId());
            }
        }
        // CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public void removeOldestOrder()
        {
            lock (_syncRoot)
            {
                oldestOrder++;
                bool removed;
                //09-04-07 Li: need lock orderTable because in CustomerReporTransaction.cs 
                //it is the underneath collecion of enumeration. We lock this which doing 
                //changed on the collection so that the enumeration will be happy.
                lock (orderTable)
                {
                    removed = orderTable.deleteFirstEntities();
                }
                if (!removed)
                {
                    //JBButil.getLog().warning(
                    Trace.WriteLineIf(JBButil.getLog().TraceWarning,
                        "District.removeOldestOrder failed for orderId="
                        + oldestOrder + ", districtId=" + districtId
                        + ", warehouseId=" + warehouseId);
                }
            }
        }
        // CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public void removeOldOrders(int minOrders)
        {
            lock (_syncRoot)
            {
                int size = orderTable.size();
                bool removed;
                //09-04-07 Li: need lock orderTable because in CustomerReporTransaction.cs 
                //it is the underneath collecion of enumeration. We lock this which doing 
                //changed on the collection so that the enumeration will be happy.
                lock (orderTable)
                {
                    removed = orderTable.deleteFirstEntities(size - minOrders);
                }
                if (removed)
                {
                    oldestOrder += size - minOrders;
                }
                else
                {
                    //JBButil.getLog().warning(
                    Trace.WriteLineIf(JBButil.getLog().TraceWarning,
                        "District.removeOldOrders failed for " + ", districtId="
                        + districtId + ", warehouseId=" + warehouseId
                        + ", size=" + orderTable.size());
                }
                //if (JBButil.getLog().isLoggable(Level.FINEST)) 
                if (JBButil.getLog().Level >= TraceLevel.Verbose)
                {
                    TraceSwitch log = JBButil.getLog();
                    Trace.WriteLineIf(log.TraceVerbose, "SteadyState trimmed orderTable to "
                        + (orderTable.size()) + " elements, from " + size
                        + " elements");
                    Trace.WriteLineIf(log.TraceVerbose, "  for districtId=" + districtId + " warehouseId="
                        + warehouseId);
                }
            }
        }
        //// CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public void removeOldNewOrders(int minNewOrders)
        {
            lock (_syncRoot)
            {
                int size = newOrderTable.size();
                bool removed;
                //09-04-07 Li: need lock newOrderTable because in DeliveryTransaction.cs 
                //it is the underneath collecion of enumeration. We lock this which doing 
                //changed on the collection so that the enumeration will be happy.
                lock (newOrderTable)
                {
                    removed = newOrderTable.deleteFirstEntities(size - minNewOrders);
                }
                if (!removed)
                {
                    //JBButil.getLog().warning(
                    Trace.WriteLineIf(JBButil.getLog().TraceWarning,
                      "District.removeOldNewOrders failed for " + ", districtId="
                      + districtId + ", warehouseId=" + warehouseId
                      + ", size=" + newOrderTable.size());
                }
                //if (JBButil.getLog().isLoggable(Level.FINEST)) 
                if (JBButil.getLog().Level >= TraceLevel.Verbose)
                {
                    TraceSwitch log = JBButil.getLog();
                    Trace.WriteLineIf(log.TraceVerbose, "SteadyState trimmed newOrderTable to "
                        + newOrderTable.size() + " elements, from " + size
                        + " elements");
                    Trace.WriteLineIf(log.TraceVerbose, "  for districtId=" + districtId + ", warehouseId="
                        + warehouseId);
                }
            }
        }
        //09-04-07 Li: need to remove the MethodImplOptions.Synchronized attribute
        //DeliveryTransaction.cs, we locked newOrderTable first then called District
        //leaving the Synchronization attribute here will cause deadlock.
        //// CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public void addNewOrder(NewOrder aNewOrder)
        {
            lock (_syncRoot)
            {
                //09-04-07 Li: need lock newOrderTable because in DeliveryTransaction.cs 
                //it is the underneath collecion of enumeration. We lock this which doing 
                //changed on the collection so that the enumeration will be happy.
                lock (newOrderTable)
                {
                    newOrderTable.put(aNewOrder.getId(), aNewOrder);
                }
            }
        }
        //09-04-07 Li: need to remove the MethodImplOptions.Synchronized attribute
        //DeliveryTransaction.cs, we locked newOrderTable first then called District
        //leaving the Synchronization attribute here will cause deadlock.
        //// CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public NewOrder removeFirstNewOrder()
        {
            lock (_syncRoot)
            {
                //09-04-07 Li: need lock newOrderTable because in DeliveryTransaction.cs 
                //it is the underneath collecion of enumeration. We lock this which doing 
                //changed on the collection so that the enumeration will be happy.
                NewOrder temp = null;
                lock (newOrderTable)
                {
                    temp = (NewOrder)newOrderTable.removeFirstElem();
                }

                return temp;
            }
        }
		// CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
		public JBBDataStorage getOrderTable() 
		{
			return orderTable;
		}

		// CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
		public String getName() 
		{
			return name;
		}

                //09-04-07 Li: need to add this for granulate lock on NewOrderTable to fix
                //enumberation problem on DeliveryTransaction.cs  
		// CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
		public JBBDataStorage getNewOrderTable() 
		{
			return newOrderTable;
		}

		// CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
		public IEnumerator newOrderIter() 
		{
			return (IEnumerator) newOrderTable.elements();
		}

        //09-04-07 Li: need to remove the MethodImplOptions.Synchronized attribute
        //DeliveryTransaction.cs, we locked newOrderTable first then called District
        //leaving the Synchronization attribute here will cause deadlock.
        //// CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public NewOrder removeNewOrder(Object key)
        {
            lock (_syncRoot)
            {
                //09-04-07 Li: the purpose of this function is to return the previous
                //value of the key in the table, then remove the key. We need to add 
                //an copy contructor for NewOrder class and add synchronization
                //to fullfill this purpose.
                lock (newOrderTable)
                {
                    NewOrder temp = (NewOrder)newOrderTable.get(key);
                    if (temp == null)
                    {
                        preVal = null;
                    }
                    else
                    {
                        preVal = new NewOrder(temp);
                        newOrderTable.remove(key);
                    }
                    return preVal;
                }
            }
        }
	}
}
