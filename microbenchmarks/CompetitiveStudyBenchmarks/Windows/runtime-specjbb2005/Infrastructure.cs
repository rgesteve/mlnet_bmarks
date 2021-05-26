/* Copyright (c) 2005 Standard Performance Evaluation Corporation (SPEC) All
 * rights reserved. 
 */
using System;
using System.Collections;

namespace Specjbb2005.src.spec.jbb
{
	/// <summary>
	/// Summary description for Infrastructure.
	/// </summary>
	internal class Infrastructure
	{
		//  "SPECjbb2005,"
		//  + "Copyright (c) 2005 Standard Performance Evaluation Corporation (SPEC),"
		//  + "All rights reserved,"
		//  + "(C) Copyright IBM Corp., 1996 - 2005"
		//  + "All rights reserved,"
		//  + "Licensed Materials - Property of SPEC";

		/*Milind: In C#, the static classes are sealed, they can contain only
		 * static fields, no instance constructors and cannot be instantiated. 
		 * Since Java supports it, I am forced to remove **static** from this class
		 * 
		*/
		private /*static*/ class SynchronizedJBBDataStorage : JBBDataStorage 
		{
			internal JBBDataStorage s;
			internal SynchronizedJBBDataStorage(JBBDataStorage s) 
			{
				this.s = s;
			}

			public bool containsKey(Object o) 
			{
				lock(s) 
				{
					return s.containsKey(o);
				}
			}

			public Object get(Object o) 
			{
				lock(s) 
				{
					return s.get(o);
				}
			}

			public Object put(Object key, Object value1) 
			{
				lock (s) 
				{
					return s.put(key, value1);
				}
			}

			public Object remove(Object o) 
			{
				lock (s) 
				{
					return s.remove(o);
				}
			}

			public int size() 
			{
				lock (s) 
				{
					return s.size();
				}
			}

			public IEnumerator elements() 
			{
				lock (s) 
				{
					return s.elements();
				}
			}
		}//private class SynchronizedJBBDataStorage : JBBDataStorage

		/*Milind: In C#, the static classes are sealed, they can contain only
		 * static fields, no instance constructors and cannot be instantiated. 
		 * Since Java supports it, I am forced to remove **static** from this class
		 * 
		*/
		private /*static*/ class SynchronizedJBBSortedStorage : SynchronizedJBBDataStorage,JBBSortedStorage
		{
			internal SynchronizedJBBSortedStorage(JBBSortedStorage s) : base (s)
			{
			}
			/*
			private static class SynchronizedJBBSortedStorage extends
								SynchronizedJBBDataStorage implements JBBSortedStorage 
							{
							SynchronizedJBBSortedStorage(JBBSortedStorage s) 
			{
				super(s);
			}*/

			public Object removeFirstElem() 
			{
				lock (s) 
				{
					return ((JBBSortedStorage) s).removeFirstElem();
				}
			}

			public bool deleteFirstEntities(int quant) 
			{
				lock (s) 
				{
					return ((JBBSortedStorage) s).deleteFirstEntities(quant);
				}
			}

			public bool deleteFirstEntities() 
			{
				lock (s) 
				{
					return ((JBBSortedStorage) s).deleteFirstEntities();
				}
			}

			public Object getMedianValue(Object firstKey, Object lastKey) 
			{
				lock (s) 
				{
					return ((JBBSortedStorage) s).getMedianValue(firstKey, lastKey);
				}
			}

            //Milind on 03/07/2007
            public void setKeyListToNull()
            {
                lock (s)
                {
                    ((JBBSortedStorage)s).setKeyListToNull();
                }
            }
            //end
		}//private static class SynchronizedJBBSortedStorage : JBBSortedStorage

		private static JBBDataStorage synchStorage(JBBDataStorage s) 
		{
			return new SynchronizedJBBDataStorage(s);
		}

		public static JBBDataStorage createStorage() 
		{
			return new MapDataStorage();
		}

		public static JBBDataStorage createStorage(bool synch) 
		{
			JBBDataStorage s = createStorage();
			if (synch) 
			{
				s = synchStorage(s);
			}
			return s;
		}

		public static JBBSortedStorage createSortedStorage() 
		{
			return new TreeMapDataStorage();
		}
	}
}
