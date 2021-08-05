/*
 * 
 * Copyright (c) 2000-2005 Standard Performance Evaluation Corporation (SPEC) All
 * rights reserved. Copyright (c) 1996-2005 IBM Corporation, Inc. All rights
 * reserved.
 */
using System;
using System.Collections;

namespace Specjbb2005.src.spec.jbb
{
	/// <summary>
	/// Summary description for TreeMapDataStorage.
	/// </summary>
	public class TreeMapDataStorage : MapDataStorage, JBBSortedStorage
	{
		/**
	 * serialversionUID of 1 for first release
	 */
		private static readonly long serialVersionUID = 1L;
        private ArrayList keyList = null;

		// This goes right after each class/interface statement
		//  static readonly String       COPYRIGHT        = "SPECjbb2005,"
		//  + "Copyright (c) 2005 Standard Performance Evaluation Corporation (SPEC),"
		//  + "All rights reserved,"
		//  + "(C) Copyright IBM Corp., 1996 - 2005"
		//  + "All rights reserved,"
		//  + "Licensed Materials - Property of SPEC";
        //  

		internal TreeMapDataStorage() : base(SortedList.Synchronized(new SortedList()))
		{
			//super(new TreeMap());
		}

        //Milind on 03/07/2007
        public void setKeyListToNull() 
        {
            //Console.WriteLine("***Inside TreeMApDataStorage.cs:: setKeyListToNull"); Console.Out.Flush();
            keyList = null;
        }
		//END
		/*
		public boolean deleteFirstEntities(int quant) {
        Iterator iter = data.values().iterator();
        for (int i = 1; i <= quant; i++) {
            if (iter.hasNext()) {
                iter.next();
                iter.remove();
            }
            else {
                return false;
            }
        }
        return true;
		}
		*/


		public bool deleteFirstEntities(int quant) 
		{
			for(int i=0;i<quant;i++) 
			{
				//data.RemoveAt(i); //see explanation below.
				((SortedList)data).RemoveAt(i);
			}
			return true ; 
		}

		/*
		 * This is what I had ported based on Java code which is above. But In .NET the IEnumerator
		 * is only for iterating through the Collection AS LONG AS THE UNDERLYING COLLECTION is not 
		 * changed(adding, modifying and deleting) but Java supports removing an item. So basically
		 * we are forced to delete using an index.
		public bool deleteFirstEntities(int quant) 
		{
			IDictionaryEnumerator en = data.GetEnumerator();
			
			for(int i=0;i<quant;i++) 
			{
				if(en.MoveNext()) 
				{
					DictionaryEntry entry = en.Entry;
					data.Remove(entry.Key);
				}
				else 
				{
					return false;
				}
			}
			return true ; 
		}*/
		
		public bool deleteFirstEntities() 
		{
			return deleteFirstEntities(1);
		}

		public Object removeFirstElem() 
		{
			int size = this.size();
			if (size > 0)
			{
				//UPGRADE_TODO: Interface 'java.util.SortedMap' was converted to 'System.Collections.SortedList' which has a different behavior. 'ms-help://MS.VSCC.2003/commoner/redir/redirect.htm?keyword="jlca1073_javautilSortedMap_3"'
				return remove(((SortedList)data).GetKey(0));
			}
			else
				return null;
			/*
			int size = this.size();
			if (size > 0) 
			{
				//TODO:First key in sorted list will be at index 0
				//In Java remove returns the previous value. Here it is void.
				//Check to see if more appropriate port?
				Object retVal = null ;
				retVal = data.GetByIndex(0);
				data.RemoveAt(0);//remove(((SortedMap) data).firstKey());
				return retVal ;
			}
			else
				return null;
			*/
		}

        //new implementation on October 19th

        public Object getMedianValue(Object firstKey, Object lastKey)
        {
            Object avgValue = null;

            ICollection coll = null;

            if (keyList == null )
            {
                coll = data.Keys;
                keyList = new ArrayList(coll);
                //Console.WriteLine("getMedianValue.KeyCount = {0}", data.Keys.Count);
            }
            
            int firstindex = keyList.BinarySearch(firstKey);
            int lastindex = keyList.BinarySearch(lastKey, StringComparer.Ordinal);

            firstindex = (firstindex < 0) ? ~firstindex : firstindex;
            lastindex = (lastindex < 0) ? ~lastindex : lastindex;
            int median_index = firstindex + ((lastindex - firstindex) + 1) / 2 - 1; //starts with 0

            avgValue = ((SortedList)data).GetByIndex(median_index);

            return avgValue;

            /*            
            SortedList tempList = new SortedList(); int i = 0;
            for (i = firstindex; i < data.Count; i++)
            {
                Object obj = ((SortedList)data).GetKey(i);
                int retVal = String.CompareOrdinal((String)obj, (String)lastKey);//Comparer.Default.Compare(obj, lastKey);
                if (retVal > 0)
                    break;
                tempList.Add(((SortedList)data).GetKey(i),
                            ((SortedList)data)[((SortedList)data).GetKey(i)]);
            }
            if (tempList.Count == 0)
            {
                Object obj1 = (i == 0) ? ((SortedList)data).GetByIndex(i) : ((SortedList)data).GetByIndex(i - 1); //for loop did index ++ and...
                //Console.WriteLine("Obj1 = {0}",obj1);
                return obj1;
            }
            IEnumerator iter = tempList.Values.GetEnumerator();
            for (int j = 1; j <= (tempList.Count + 1) / 2; j++)
            {
                iter.MoveNext();
                avgValue = (object)iter.Current;
            }
            //Console.WriteLine("avgValue = {0}", avgValue);
             
            return avgValue;*/
        }
        //new implementation on October 17th.

        /*
        public Object getMedianValue(Object firstKey, Object lastKey)
        {
            int index = 0;
            Object avgValue = null;
            //Comparer comparer = Comparer.Default ;

            SortedList tempList = new SortedList();
            
            if (data.Count > 0 && !firstKey.Equals(lastKey))
            {
                //while (Comparer.Default.Compare(((SortedList)data).GetKey(index),firstKey) < 0)
                //while (String.CompareOrdinal((String)((SortedList)data).GetKey(index), (String)firstKey) < 0)
                while( ((String)((SortedList)data).GetKey(index)).StartsWith((String)firstKey))
                    index++; //advance till we go to the first key.

                for (; index < data.Count; index++)
                {
                    Object obj = ((SortedList)data).GetKey(index) ;
                    int retVal = String.CompareOrdinal((String)obj,(String)lastKey);//Comparer.Default.Compare(obj, lastKey);
                    if(retVal > 0)
                        break;
                    tempList.Add(((SortedList)data).GetKey(index), 
                                ((SortedList)data)[((SortedList)data).GetKey(index)]);
                }
            }
            if (tempList.Count == 0)
            {
                Object obj1 = (index == 0) ? ((SortedList)data).GetByIndex(index) : ((SortedList)data).GetByIndex(index-1); //for loop did index ++ and...
                //Console.WriteLine("Obj1 = {0}",obj1);
                return obj1;
            }
            
            IEnumerator iter = tempList.Values.GetEnumerator();
            for (int i = 1; i <= (tempList.Count + 1) / 2; i++)
            {
                iter.MoveNext();
                avgValue = (object)iter.Current;
            }
            //Console.WriteLine("avgValue = {0}", avgValue);
            return avgValue;
        }*/
        /*
        public Object getMedianValue(Object firstKey, Object lastKey) 
        {
			
            Object avgValue = null;
            int startindex = ((SortedList)data).IndexOfKey(firstKey) ;
            int endindex   = ((SortedList)data).IndexOfKey(lastKey) ;
		
            avgValue = ((SortedList)data).GetByIndex((endindex-startindex)+1);	
            //commented otu on 17th oct just to get it goiong
            //avgValue = ((SortedList)data).GetByIndex(startindex + ((endindex - startindex) + 1) / 2);//MPH.Oct 14 
            return avgValue ;
        }
        */
        /* This is wrong implementation. This gives median value of entire list
         * and not the sub list (starting from firstkey - lastkey).
        public Object getMedianValue(Object firstKey, Object lastKey) 
        {
            Object avgValue = null;
            int Size = data.Count;
            IEnumerator en = data.Values.GetEnumerator();
            for (int i = 1; i <= (Size + 1) / 2; i++) 
            {
                if(en.MoveNext()) 
                {
                    avgValue = en.Current;
                } 
                else 
                {
                    Console.WriteLine("\nError occurred TreeMapDataStorage::getMedianValue\n");
                }
            }
            return avgValue;
        }
        */
    }
}
