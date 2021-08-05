/* Copyright (c) 2000-2005 Standard Performance Evaluation Corporation (SPEC)
 * All rights reserved.
 * Copyright (c) 1996-2005 IBM Corporation, Inc. All rights reserved.
 */
using System;
using System.Runtime.CompilerServices;

namespace Specjbb2005.src.spec.jbb
{
	/// <summary>
	/// Summary description for Customer.
	/// </summary>
	public class Customer
	{
		// This goes right after each class/interface statement
		//  internal static readonly String         COPYRIGHT   = "SPECjbb2005,"
		//  + "Copyright (c) 2000-2005 Standard Performance Evaluation Corporation (SPEC),"
		//  + "All rights reserved,"
		//  + "(C) Copyright IBM Corp., 1996 - 2005"
		//  + "All rights reserved,"
		//  + "Licensed Materials - Property of SPEC";

		private static readonly String bad_credit  = "BC";

		private static readonly String good_credit = "GC";

		private Order               lastOrder;

		// required data
		private String              firstName;

		private String              middleName;

		private String              lastName;

		private Address             address;

		private String              phone;

		private DateTime            since;

		private String              data;

		private Decimal          creditLimit;

		private Decimal          discount;

		private Decimal          balance;

		private Decimal          ytd;

		private char                credit1;

		private char                credit2;

		private short               customerId;

		private short               paymentCount;

		private short               deliveryCount;

		private sbyte                districtId;

		private short               warehouseId;

        private readonly object _syncRoot = new Object(); // CORECLR

        public Customer()
		{
			address = new Address();
			since = DateTime.Now;//new DateTime();
			creditLimit = Decimal.Zero ;//BigDecimal.valueOf(0, 2);
			balance = Decimal.Zero ;//BigDecimal.valueOf(0, 2);
			ytd = Decimal.Zero ;//BigDecimal.valueOf(0, 2);
			paymentCount = 0;
			deliveryCount = 0;
			lastOrder = null;
		}

		public short getId() 
		{
			return customerId;
		}

        // CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public void display()
        {
            lock (_syncRoot)
            {
                Console.WriteLine("Customer Display *********************************");
                Console.WriteLine("customerId =================> " + customerId);
                Console.WriteLine("district ID ========> " + districtId);
                Console.WriteLine("warehouse ID========> " + warehouseId);
                Console.WriteLine("firstname===========> " + firstName);
                Console.WriteLine("middlename =========> " + middleName);
                Console.WriteLine("lastname ===========> " + lastName);
                address.display();
                Console.WriteLine("END ****Customer Display *********************************");
            }
        }
        // CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public void addOrder(Order thisOrder)
        {
            lock (_syncRoot)
            {
                lastOrder = thisOrder;
            }
        }
        // CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public Order getLatestOrder()
        {
            lock (_syncRoot)
            {
                Order order = lastOrder;
                return order;
            }
        }
		public String getFirstName() 
		{
			return firstName;
		}

		public String getMiddleName() 
		{
			return middleName;
		}

		public String getLastName() 
		{
			return lastName;
		}

		public Address getAddress() 
		{
			return address;
		}

		public Decimal getDiscountRate() 
		{
			return discount;
		}

		public String getCreditStatus() 
		{
			String result;
			if (credit1 == 'G')
				result = good_credit;
			else
				result = bad_credit;
			return result;
		}

		public short getWarehouseId() 
		{
			return warehouseId;
		}

		public sbyte getDistrictId() 
		{
			return districtId;
		}

        // CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public Decimal getBalance()
        {
            lock (_syncRoot)
            {
                Decimal temp = balance;
                return temp;
            }
        }
        // CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public Decimal getCreditLimit()
        {
            lock (_syncRoot)
            {
                return creditLimit;
            }
        }
        // CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public void adjustBalance(Decimal amount)
        {
            lock (_syncRoot)
            {
                balance = Decimal.Add(balance, amount);//balance.Add(amount);
            }
        }
        // CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public void increaseYTD(Decimal amount)
        {
            lock (_syncRoot)
            {
                ytd = Decimal.Add(ytd, amount);//ytd.Add(amount);
            }
        }
        // CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public void incrementPaymentCount()
        {
            lock (_syncRoot)
            {
                ++paymentCount;
            }
        }
        // CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public void incrementDeliveryCount()
        {
            lock (_syncRoot)
            {
                ++deliveryCount;
            }
        }
        // CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public void updateCustomerData(String newData)
        {
            lock (_syncRoot)
            {
                String oldData;
                // The data must be added on the left and the old data shifted right.
                oldData = data;
                if ((oldData.Length + newData.Length) <= 500)
                {
                    // CORECLR data = String.Copy(newData+data);//new String((newData + data));
                    data = newData + data;
                }
                else
                {
                    String shiftData = oldData.Substring(0, (500 - newData.Length));
                    // CORECLR data = String.Copy(newData+shiftData); //new String((newData + shiftData));
                    data = newData + shiftData;
                }
            }
        }
        // CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public void setUsingRandom(short inCustomerId, short inWarehouseId, sbyte inDistrictId)
        {
            lock (_syncRoot)
            {
                customerId = inCustomerId;
                districtId = inDistrictId;
                warehouseId = inWarehouseId;
                lastName = JBButil.create_random_last_name(inCustomerId, warehouseId);
                firstName = new String(JBButil.create_random_a_string(8, 16, warehouseId));
                middleName = "OE"; // CORECLR String.Copy("OE");
                address.setUsingRandom(warehouseId);
                phone = new String(JBButil.create_random_n_string(16, 16, warehouseId));//new String(JBButil.create_random_n_string(16, 16, warehouseId));
                if (JBButil.random(1, 10, warehouseId) > 1)
                {
                    credit1 = 'G';
                    credit2 = 'C';
                }
                else
                {
                    credit1 = 'B';
                    credit2 = 'C';
                }
                creditLimit = new Decimal(5000000);//new Decimal(5000000,0,0,false,2); //Decimal.valueOf(5000000, 2);
                float temp = JBButil.create_random_float_val_return(0.0f, 0.5000f, 0.0001f, warehouseId);
                discount = Convert.ToDecimal(temp); //new Decimal(temp) ;
                                                    //Milind
                                                    // CORECLR discount = Decimal.Round(discount,2);//WE want only 2 decimals.
                discount = Math.Round(discount, 2);//WE want only 2 decimals.

                //Console.WriteLine("Customer ID = {0} : Discount Value = {1}",customerId, discount);
                //discount = new BigDecimal(temp).setScale(4, BigDecimal.ROUND_HALF_UP);
                balance = new Decimal(1000, 0, 0, true, 2);//BigDecimal.valueOf(-1000L, 2);
                ytd = new Decimal(1000, 0, 0, false, 2);//Decimal.valueOf(1000L, 2);
                paymentCount = 1;
                deliveryCount = 0;
                data = new String(JBButil.create_random_a_string(300, 500, warehouseId));
            }
        }
		public String getPhone() 
		{
			return phone;
		}

		public DateTime getSince() 
		{
			return since;
		}

        // CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public String getCustomerDataParts()
        {
            lock (_syncRoot)
            {
                String temp = data;
                return temp;
            }
        }
	}
}
