#region License
/* 
 *
 * Open3270 - A C# implementation of the TN3270/TN3270E protocol
 *
 * Copyright (c) 2004-2020 Michael Warriner
 * Modifications (c) as per Git change history
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy of this software
 * and associated documentation files (the "Software"), to deal in the Software without restriction,
 * including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense,
 * and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so,
 * subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in all copies or substantial 
 * portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT 
 * LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. 
 * IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, 
 * WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE 
 * SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
 */
 
#endregion
using System;
using System.Threading;

namespace Open3270.Library
{
	/// <summary>
	/// Author: William Stacey (staceyw@mvps.org)
	/// Date: 06/10/04
	/// The Dijkstra semaphore (also called a counting
	/// semaphore) is used to control access to
	/// a set of resources. A Dijkstra semaphore
	/// has a count associated with it and each
	/// Acquire() call reduces the count. A thread
	/// that tries to Acquire() the semaphore
	/// with a zero count blocks until someone else
	/// calls Release() to increase the count.
	/// <seealso cref="http://www.fawcette.com/javapro/2002_02/magazine/features/krangaraju/"/>
	/// <seealso cref="http://www.mcs.drexel.edu/~shartley/MCS361/Lectures/designingJavaSemaphore.html"/>
	/// </summary>
	internal sealed class MySemaphore
	{
		#region Fields
        private int initialCount; // CFCJR
		// Current count available.
		private int count;
		// Max slots in the semaphore.
		private int maxCount;
		// Object used for sync.
		private readonly object syncLock;
		// Object used for starvation sync.
		private readonly object starvationLock;
		#endregion

		#region Constructors
		/// <summary>
		/// Creates semaphore object with a maxCount
		/// and set initial count to maxCount.
		/// </summary>
		/// <param name="maxCount">
		/// Maximum count for the semaphore object.
		/// This value must be greater than zero.
		/// </param>
		public MySemaphore(int maxCount) : this(maxCount, maxCount)
		{
		}

		/// <summary>
		/// Creates semaphore object with
		/// a maximum count and initial count.
		/// </summary>
		/// <param name="initialCount">
		/// Initial count for the semaphore object.
		/// This value must be zero or greater
		/// and less than or equal to maximumCount.
		/// </param>
        /// <param name="maxCount">
		/// Maximum count for the semaphore object.
		/// This value must be greater than zero.
		/// </param>
		public MySemaphore(int initialCount, int maxCount)
		{
			if ( initialCount < 0 )
				throw new 
					ArgumentOutOfRangeException("initialCount", "initialCount must be >= 0.");
			if ( maxCount < 1 )
				throw new ArgumentOutOfRangeException("maxCount", "maxCount must be >= 1.");
			if ( initialCount > maxCount)
				throw new 
					ArgumentOutOfRangeException("initialCount", "initialCount" + 
					" must be <= maxCount.");
			count = initialCount;
            this.initialCount = initialCount; // CFCJR
			this.maxCount = maxCount;
			syncLock = new object();
			starvationLock = new object();
		}

		#endregion

		#region Properties
		/// <summary>
		/// Gets the current available count (or slots)
		/// in the semaphore. A count of zero means that no slots
		/// are available and calls to Acquire will block until
		/// other thread(s) call Release.
		/// Example:
		/// A semaphore with a count of 2 will allow
		/// 2 more Acquire calls before blocking.
		/// </summary>
		public int Count
		{
			get
			{
				lock(syncLock)
				{
					return count;
				}
			}
		}

		/// <summary>
		/// Gets the maximum count of the semaphore
		/// set during construction.
		/// </summary>
		public int MaxCount
		{
			get { return maxCount; }
		}

		#endregion

		#region Public Methods

		/// <summary>
        /// Resets the semaphore to it's initial count
        /// </summary>
        public void Reset() //CFCJR
        {
            lock (syncLock)
            {
                count = initialCount;
                Monitor.PulseAll(syncLock);
            }
        }

		/// <summary>
		/// Acquires semaphore and decrements count by 1.
		/// If count is zero, this will
		/// block indefinitely until another thread executes
		/// a Release() to increase the count.
		/// </summary>
		/// <returns>true if the call returned because
		/// the caller reacquired the lock for the
		/// specified object. This method does not return
		/// if the lock is not reacquired.</returns>
		public bool Acquire()
		{
			return Acquire(Timeout.Infinite);
		}

		/// <summary>
		/// Returns a value indicating if Semephore
		/// can be acquired within the timeout period.
		/// </summary>
		/// <returns>true if the lock was acquired before
		/// the specified time elapsed; otherwise, false.</returns>
		/// <exception cref="ArgumentOutOfRangeException">
		/// The value of the millisecondsTimeout parameter
		/// is negative, and is not equal to Infinite.
		/// </exception>
		public bool Acquire(int millisecondsTimeout)
		{
			lock(syncLock)
			{
				// Use spin lock instead of an if test, to handle
				// rogue/barging threads that can enter
				// syncLock before a thread that was notified by a pulse.
				// That rogue thread would
				// decrease the count, then our "Pulsed" thread
				// would regain the lock and continue and
				// decrease the count to -1 which is an error.
				// The while loop/test prevents this.
                while (count == 0)
                {
					try
					{
						if (!Monitor.Wait(syncLock, millisecondsTimeout))
							return false;
					}
					catch
					{
						// If we get interupted or aborted,
						// we may have been pulsed before.
						// If we just exit, that pulse would get lost and
						// possibly result in a "live" lock
						// where other threads are waiting
						// on syncLock, and never get a pulse.
						// Regenerate a Pulse as we consumed it.
						// Even if we did not get
						// pulsed, this does not hurt as any thread
						// will check again for count = 0.
						Monitor.Pulse(syncLock);
						// Rethrow the exception for caller.
						// Now semaphore state is same as if
						// this call never happened. Caller must
						// decide how to handle exception.
						throw;
					}
                }
				count--;
				if ( count == 0 )
					lock(starvationLock) { Monitor.PulseAll(starvationLock); }
				return true;
			}
		}

		/// <summary>
		/// Acquires all the semaphores and brings
		/// count to zero. This has the effect
		/// of block other threads until we release one or more slots.
		/// <seealso cref="Acquire()"/>
		/// <seealso cref="ReleaseAll()"/>
		/// </summary>
		/// <returns>true if the acquired maxCount slots.
		/// This method does not return until
		/// all slots are acquired.</returns>
		public bool AcquireAll()
		{
			// Aquires all slots or blocks for Timeout.Infinite.
			return AcquireAll(Timeout.Infinite);
		}

		/// <summary>
		/// Tries to acquire (maxCount) slots
		/// in semaphore. If any single attempt to
		/// acquire a semaphore slot exceeds
		/// millisecondsTimeout, then return is false.
		/// Return is true if we acquire maxCount slots.
		/// Normally this method would be paired
		/// with the ReleaseAll method.
		/// </summary>
		/// <param name="millisecondsTimeout"></param>
		/// <returns>true if maxCount slots are acquired
		/// before the specified time elapsed;
		/// otherwise, false.</returns>
		public bool AcquireAll(int millisecondsTimeout)
		{
			int slotsGot = 0;
			int elapsedMS = 0;
			DateTime start = DateTime.Now;
			int timeout = millisecondsTimeout;
			for (int i = 0; i < maxCount; i++)
			{
				try
				{
					if (! Acquire(timeout) )
					{
						// Could not acquire all slots,
						// release any we may already have got.
						if ( slotsGot > 0 )
							Release(slotsGot);
						return false;
					}
					else
					{
                        if (timeout > 0) // if not Timeout.Infinite
                        {
						elapsedMS = (int)((TimeSpan)
							(DateTime.Now - start)).TotalMilliseconds;
						timeout = millisecondsTimeout - elapsedMS;
						// Next wait will be a smaller timeout.

                            if (timeout < 0)
							timeout = 0;
						// Next Acquire will return
						// false if we have to wait;
                        }

						slotsGot++;
						// If we get all remaining slots
						// with no timeout, we just keep going.
					}
				}
				catch
				{
					// Catch any exception during Acquire wait.
					if ( slotsGot > 0 )
						Release(slotsGot);
					throw;
				}
			} // end for.
			// Count is now zero, so notify any/all starvation consumers.
			lock(starvationLock) { Monitor.PulseAll(starvationLock); }
			return true;
		}

		/// <summary>
		/// Increases the count of the semaphore object by one.
		/// </summary>
		public void Release()
		{
			Release(1);
		}

		/// <summary>
		/// Increases the count of the semaphore
		/// object by a specified amount.
		/// </summary>
		/// <param name="count">Amount by which the semaphore
		/// object's current count is to be increased.</param>
		/// <exception cref="ArgumentOutOfRangeException">
		/// The releaseCount must be one or greater.
		/// </exception>
		/// <exception cref="ArgumentOutOfRangeException">
		/// The releaseCount would cause
		/// the semaphore's count to exceed maxCount. 
		/// </exception>
		public void Release(int releaseCount)
		{
			if ( releaseCount < 1 )
				throw new 
					ArgumentOutOfRangeException("releaseCount", "releaseCount must be >= 1.");

			lock(syncLock)
			{
				if ( (count + releaseCount) > maxCount )
					throw new 
						ArgumentOutOfRangeException("releaseCount", "releaseCount" + 
						" would cause the semaphore's count to exceed maxCount.");
				count += releaseCount;
				Monitor.PulseAll(syncLock);
			}
		}

		/// <summary>
		/// Returns indication if we could
		/// release one slot in the semaphore.
		/// </summary>
		/// <returns>true if we released
		/// one slot; otherwise false.</returns>
		public bool TryRelease()
		{
			return TryRelease(1);
		}

		/// <summary>
		/// Returns indication if we could release
		/// releaseCount slots in the semaphore.
		/// </summary>
		/// <param name="releaseCount"></param>
		/// <returns>true if we released releaseCount
		/// slots; otherwise false.</returns>
		public bool TryRelease(int releaseCount)
		{
			if ( releaseCount <= 0 )
				return false;

			lock(syncLock)
			{
				if ( (count + releaseCount) > maxCount )
					return false;
				else
					count += releaseCount;
				Monitor.PulseAll(syncLock);
				return true;
			}
		}

		/// <summary>
		/// Releases all remaining semaphores
		/// not currently owned. This would normally be
		/// called by a thread that previously
		/// called AcquireAll(). Note:  Be carefull when
		/// using this method as it will release
		/// all threads waiting on an Aquire method,
		/// which may or may not be what you want.
		/// An alternative would be to spin on
		/// TryRelease() until it returns false.
		/// </summary>
		public void ReleaseAll()
		{
			lock(syncLock)
			{
				count = maxCount;
				Monitor.PulseAll(syncLock);
				// We PulseAll instead of calling pulse
				// with exact number of times needed.
				// This can be slightly inefficient,
				// but is safe and simple.
				// See http://www.mcs.drexel.edu/~shartley/
				//   MCS361/Lectures/designingJavaSemaphore.html
			}
		}

		/// <summary>
		/// This method blocks the calling thread
		/// until the semaphore count drops to zero.
		/// A drop to zero will not be recognized
		/// if a release happens before this call.
		/// You can use this to get notified when
		/// semephore's count reaches zero.  This
		/// is also known as a "reverse-sensing" semaphore.
		/// </summary>
		public void WaitForStarvation()
		{
			lock(starvationLock)
			{
				// We will block until count is 0.
				// We use Interlocked just to be sure
				// we test for zero correctly as we
				// are not in the syncLock context.
				if ( Interlocked.CompareExchange(ref count, 0, 0) != 0 )
					Monitor.Wait(starvationLock);
				// Any Exception during wait will
				// just go to caller.  Do not need to signal
				// any other threads as PulseAll(starvationLock) is used.
				// Also note we don't do a spin
				// while() test as we only care that 
				// count *did go to zero at some instant.
			}
		}
		#endregion
	} // class SemephoreDijkstra
}