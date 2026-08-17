//
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.
//

using System;
using System.IO;
using System.Threading.Tasks;
using System.Web.SessionState;
using Microsoft.Web.Redis.Tests;
using StackExchange.Redis;
using Xunit;

namespace Microsoft.Web.Redis.FunctionalTests
{
    public class RedisConnectionWrapperFunctionalTests
    {
        private static int uniqueSessionNumber = 1;

        private RedisConnectionWrapper GetRedisConnectionWrapperWithUniqueSession()
        {
            return GetRedisConnectionWrapperWithUniqueSession(Utility.GetDefaultConfigUtility());
        }

        private RedisConnectionWrapper GetRedisConnectionWrapperWithUniqueSession(ProviderConfiguration pc)
        {
            string id = Guid.NewGuid().ToString();
            uniqueSessionNumber++;
            // Initial connection with redis
            RedisConnectionWrapper.sharedConnection = null;
            RedisConnectionWrapper redisConn = new RedisConnectionWrapper(pc, id);
            return redisConn;
        }

        private void DisposeRedisConnectionWrapper(RedisConnectionWrapper redisConn)
        {
            RedisConnectionWrapper.sharedConnection = null;
        }

        [Fact]
        public async Task Set_ValidData_WithCustomSerializer()
        {
            // this also tests host:port config part
            ProviderConfiguration pc = Utility.GetDefaultConfigUtility();
            pc.ApplicationName = "APPTEST";
            pc.Port = 6379;

            using (RedisServer redisServer = new RedisServer())
            {
                RedisConnectionWrapper redisConn = GetRedisConnectionWrapperWithUniqueSession(pc);

                // Inserting data into redis server
                SessionStateItemCollection data = new SessionStateItemCollection();
                data["key"] = "value";
                data["key1"] = "value1";
                await redisConn.SetAsync(data, 900);

                // Get actual connection and get data blob from redis
                IDatabase actualConnection = GetRealRedisConnection(redisConn);

                RedisValue sessionDataFromRedis = actualConnection.StringGet(redisConn.Keys.DataKey);
                SessionStateItemCollection dataFromRedis = null;
                MemoryStream ms = new MemoryStream(sessionDataFromRedis);
                BinaryReader reader = new BinaryReader(ms);
                dataFromRedis = SessionStateItemCollection.Deserialize(reader);

                Assert.Equal("value", dataFromRedis["key"]);
                Assert.Equal("value1", dataFromRedis["key1"]);

                // remove data from redis
                actualConnection.KeyDelete(redisConn.Keys.DataKey);
                DisposeRedisConnectionWrapper(redisConn);
            }
        }

        [Fact]
        public async Task Set_ValidData()
        {
            // this also tests host:port config part
            ProviderConfiguration pc = Utility.GetDefaultConfigUtility();
            pc.ApplicationName = "APPTEST";
            pc.Port = 6379;

            using (RedisServer redisServer = new RedisServer())
            {
                RedisConnectionWrapper redisConn = GetRedisConnectionWrapperWithUniqueSession(pc);

                // Inserting data into redis server
                SessionStateItemCollection data = new SessionStateItemCollection();
                data["key"] = "value";
                data["key1"] = "value1";
                await redisConn.SetAsync(data, 900);

                // Get actual connection and get data blob from redis
                IDatabase actualConnection = GetRealRedisConnection(redisConn);

                RedisValue sessionDataFromRedis = actualConnection.StringGet(redisConn.Keys.DataKey);
                SessionStateItemCollection dataFromRedis = null;
                MemoryStream ms = new MemoryStream(sessionDataFromRedis);
                BinaryReader reader = new BinaryReader(ms);
                dataFromRedis = SessionStateItemCollection.Deserialize(reader);

                Assert.Equal("value", dataFromRedis["key"]);
                Assert.Equal("value1", dataFromRedis["key1"]);

                // remove data from redis
                actualConnection.KeyDelete(redisConn.Keys.DataKey);
                DisposeRedisConnectionWrapper(redisConn);
            }
        }

        [Fact]
        public async Task Set_NullData()
        {
            // this also tests host:port config part
            ProviderConfiguration pc = Utility.GetDefaultConfigUtility();
            pc.ApplicationName = "APPTEST";
            pc.Port = 6379;

            using (RedisServer redisServer = new RedisServer())
            {
                RedisConnectionWrapper redisConn = GetRedisConnectionWrapperWithUniqueSession(pc);

                // Inserting data into redis server
                SessionStateItemCollection data = new SessionStateItemCollection();
                data["key"] = "value";
                data["key1"] = null;
                await redisConn.SetAsync(data, 900);

                // Get actual connection and get data blob from redis
                IDatabase actualConnection = GetRealRedisConnection(redisConn);

                RedisValue sessionDataFromRedis = actualConnection.StringGet(redisConn.Keys.DataKey);
                SessionStateItemCollection dataFromRedis = null;
                MemoryStream ms = new MemoryStream(sessionDataFromRedis);
                BinaryReader reader = new BinaryReader(ms);
                dataFromRedis = SessionStateItemCollection.Deserialize(reader);

                Assert.Equal("value", dataFromRedis["key"]);
                Assert.Null(dataFromRedis["key1"]);

                // remove data from redis
                actualConnection.KeyDelete(redisConn.Keys.DataKey);
                DisposeRedisConnectionWrapper(redisConn);
            }
        }

        [Fact]
        public async Task Set_ExpireData()
        {
            ProviderConfiguration pc = Utility.GetDefaultConfigUtility();
            using (RedisServer redisServer = new RedisServer())
            {
                RedisConnectionWrapper redisConn = GetRedisConnectionWrapperWithUniqueSession();
                // Inserting data into redis server that expires after 1 second
                SessionStateItemCollection data = new SessionStateItemCollection();
                data["key"] = "value";
                await redisConn.SetAsync(data, 1);

                // Wait for 2 seconds so that data will expire
                System.Threading.Thread.Sleep(1100);

                // Get actual connection and get data blob from redis
                IDatabase actualConnection = GetRealRedisConnection(redisConn);
                HashEntry[] sessionDataFromRedis = actualConnection.HashGetAll(redisConn.Keys.DataKey);

                // Check that data shoud not be there
                Assert.Empty(sessionDataFromRedis);
                DisposeRedisConnectionWrapper(redisConn);
            }
        }

        [Fact]
        public async Task TryTakeWriteLockAndGetData_WithNullData()
        {
            ProviderConfiguration pc = Utility.GetDefaultConfigUtility();
            using (RedisServer redisServer = new RedisServer())
            {
                RedisConnectionWrapper redisConn = GetRedisConnectionWrapperWithUniqueSession();

                // Inserting data into redis server
                SessionStateItemCollection data = new SessionStateItemCollection();
                data["key"] = null;
                await redisConn.SetAsync(data, 900);

                DateTime lockTime = DateTime.Now;
                const int lockTimeout = 900;
                WriteLockData lockData = await redisConn.TryTakeWriteLockAndGetDataAsync(lockTime, lockTimeout);
                Assert.True(lockData.IsLockTaken);
                Assert.Equal(lockTime.Ticks.ToString(), lockData.LockId);
                Assert.Single(lockData.Data);
                Assert.Null(lockData.Data["key"]);

                // Get actual connection and get data lock from redis
                IDatabase actualConnection = GetRealRedisConnection(redisConn);
                string lockValueFromRedis = actualConnection.StringGet(redisConn.Keys.LockKey);
                Assert.Equal(lockTime.Ticks.ToString(), lockValueFromRedis);

                // remove data and lock from redis
                actualConnection.KeyDelete(redisConn.Keys.DataKey);
                actualConnection.KeyDelete(redisConn.Keys.LockKey);
                DisposeRedisConnectionWrapper(redisConn);
            }
        }

        [Fact]
        public async Task TryTakeWriteLockAndGetData_WriteLockWithoutAnyOtherLock()
        {
            ProviderConfiguration pc = Utility.GetDefaultConfigUtility();
            using (RedisServer redisServer = new RedisServer())
            {
                RedisConnectionWrapper redisConn = GetRedisConnectionWrapperWithUniqueSession();

                // Inserting data into redis server
                SessionStateItemCollection data = new SessionStateItemCollection();
                data["key"] = "value";
                await redisConn.SetAsync(data, 900);

                DateTime lockTime = DateTime.Now;
                const int lockTimeout = 900;
                WriteLockData lockData = await redisConn.TryTakeWriteLockAndGetDataAsync(lockTime, lockTimeout);
                Assert.True(lockData.IsLockTaken);
                Assert.Equal(lockTime.Ticks.ToString(), lockData.LockId);

                Assert.Single(lockData.Data);

                // this will desirialize value
                Assert.Equal("value", lockData.Data["key"]);

                // Get actual connection and get data lock from redis
                IDatabase actualConnection = GetRealRedisConnection(redisConn);
                string lockValueFromRedis = actualConnection.StringGet(redisConn.Keys.LockKey);
                Assert.Equal(lockTime.Ticks.ToString(), lockValueFromRedis);

                // remove data and lock from redis
                actualConnection.KeyDelete(redisConn.Keys.DataKey);
                actualConnection.KeyDelete(redisConn.Keys.LockKey);
                DisposeRedisConnectionWrapper(redisConn);
            }
        }

        [Fact]
        public async Task TryTakeWriteLockAndGetData_WriteLockWithOtherWriteLock()
        {
            ProviderConfiguration pc = Utility.GetDefaultConfigUtility();
            using (RedisServer redisServer = new RedisServer())
            {
                RedisConnectionWrapper redisConn = GetRedisConnectionWrapperWithUniqueSession();

                // Inserting data into redis server
                SessionStateItemCollection data = new SessionStateItemCollection();
                data["key"] = "value";
                await redisConn.SetAsync(data, 900);

                const int lockTimeout = 900;

                // Take write lock successfully first time
                DateTime lockTime1 = DateTime.Now;
                WriteLockData lockData1 = await redisConn.TryTakeWriteLockAndGetDataAsync(lockTime1, lockTimeout);
                Assert.True(lockData1.IsLockTaken);
                Assert.Equal(lockTime1.Ticks.ToString(), lockData1.LockId);
                Assert.Single(lockData1.Data);

                // try to take write lock and fail and get earlier lock id
                DateTime lockTime2 = lockTime1.AddSeconds(1);
                WriteLockData lockData2 = await redisConn.TryTakeWriteLockAndGetDataAsync(lockTime2, lockTimeout);
                Assert.False(lockData2.IsLockTaken);
                Assert.Equal(lockTime1.Ticks.ToString(), lockData2.LockId);
                Assert.Null(lockData2.Data);

                // Get actual connection
                IDatabase actualConnection = GetRealRedisConnection(redisConn);
                // remove data and lock from redis
                actualConnection.KeyDelete(redisConn.Keys.DataKey);
                actualConnection.KeyDelete(redisConn.Keys.LockKey);
                DisposeRedisConnectionWrapper(redisConn);
            }
        }

        [Fact]
        public async Task TryTakeWriteLockAndGetData_WriteLockWithOtherWriteLockWithSameLockId()
        {
            ProviderConfiguration pc = Utility.GetDefaultConfigUtility();
            using (RedisServer redisServer = new RedisServer())
            {
                RedisConnectionWrapper redisConn = GetRedisConnectionWrapperWithUniqueSession();

                // Inserting data into redis server
                SessionStateItemCollection data = new SessionStateItemCollection();
                data["key"] = "value";
                await redisConn.SetAsync(data, 900);

                int lockTimeout = 900;
                // Same LockId
                DateTime lockTime = DateTime.Now;

                // Take write lock successfully first time
                WriteLockData lockData1 = await redisConn.TryTakeWriteLockAndGetDataAsync(lockTime, lockTimeout);
                Assert.True(lockData1.IsLockTaken);
                Assert.Equal(lockTime.Ticks.ToString(), lockData1.LockId);
                Assert.Single(lockData1.Data);

                // try to take write lock and fail and get earlier lock id
                WriteLockData lockData2 = await redisConn.TryTakeWriteLockAndGetDataAsync(lockTime, lockTimeout);
                Assert.False(lockData2.IsLockTaken);
                Assert.Equal(lockTime.Ticks.ToString(), lockData2.LockId);
                Assert.Null(lockData2.Data);

                // Get actual connection
                IDatabase actualConnection = GetRealRedisConnection(redisConn);
                // remove data and lock from redis
                actualConnection.KeyDelete(redisConn.Keys.DataKey);
                actualConnection.KeyDelete(redisConn.Keys.LockKey);
                DisposeRedisConnectionWrapper(redisConn);
            }
        }

        [Fact]
        public async Task TryTakeReadLockAndGetData_WithoutAnyLock()
        {
            ProviderConfiguration pc = Utility.GetDefaultConfigUtility();
            using (RedisServer redisServer = new RedisServer())
            {
                RedisConnectionWrapper redisConn = GetRedisConnectionWrapperWithUniqueSession();

                // Inserting data into redis server
                SessionStateItemCollection data = new SessionStateItemCollection();
                data["key"] = "value";
                await redisConn.SetAsync(data, 900);

                WriteLockData lockData = await redisConn.TryCheckWriteLockAndGetDataAsync();
                Assert.True(lockData.IsLockTaken);
                Assert.Null(lockData.LockId);
                Assert.Single(lockData.Data);
                Assert.Equal("value", lockData.Data["key"]);

                // Get actual connection
                // remove data from redis
                IDatabase actualConnection = GetRealRedisConnection(redisConn);
                actualConnection.KeyDelete(redisConn.Keys.DataKey);
                DisposeRedisConnectionWrapper(redisConn);
            }
        }

        [Fact]
        public async Task TryTakeReadLockAndGetData_WithOtherWriteLock()
        {
            ProviderConfiguration pc = Utility.GetDefaultConfigUtility();
            using (RedisServer redisServer = new RedisServer())
            {
                RedisConnectionWrapper redisConn = GetRedisConnectionWrapperWithUniqueSession();

                // Inserting data into redis server
                SessionStateItemCollection data = new SessionStateItemCollection();
                data["key"] = "value";
                await redisConn.SetAsync(data, 900);

                const int lockTimeout = 900;

                DateTime lockTime1 = DateTime.Now;
                WriteLockData lockData1 = await redisConn.TryTakeWriteLockAndGetDataAsync(lockTime1, lockTimeout);
                Assert.True(lockData1.IsLockTaken);
                Assert.Equal(lockTime1.Ticks.ToString(), lockData1.LockId);
                Assert.Single(lockData1.Data);

                WriteLockData lockData2 = await redisConn.TryCheckWriteLockAndGetDataAsync();
                Assert.False(lockData2.IsLockTaken);
                Assert.Equal(lockTime1.Ticks.ToString(), lockData2.LockId);
                Assert.Null(lockData2.Data);

                // Get actual connection
                // remove data and lock from redis
                IDatabase actualConnection = GetRealRedisConnection(redisConn);
                actualConnection.KeyDelete(redisConn.Keys.DataKey);
                actualConnection.KeyDelete(redisConn.Keys.LockKey);
                DisposeRedisConnectionWrapper(redisConn);
            }
        }

        [Fact]
        public async Task TryTakeWriteLockAndGetData_ExpireWriteLock()
        {
            ProviderConfiguration pc = Utility.GetDefaultConfigUtility();
            using (RedisServer redisServer = new RedisServer())
            {
                RedisConnectionWrapper redisConn = GetRedisConnectionWrapperWithUniqueSession();

                // Inserting data into redis server
                SessionStateItemCollection data = new SessionStateItemCollection();
                data["key"] = "value";
                await redisConn.SetAsync(data, 900);

                int lockTimeout = 1;

                DateTime lockTime = DateTime.Now;
                WriteLockData lockData = await redisConn.TryTakeWriteLockAndGetDataAsync(lockTime, lockTimeout);
                Assert.True(lockData.IsLockTaken);
                Assert.Equal(lockTime.Ticks.ToString(), lockData.LockId);
                Assert.Single(lockData.Data);

                // Wait for 2 seconds so that lock will expire
                await Task.Delay(1100);

                // Get actual connection and check that lock does not exist
                IDatabase actualConnection = GetRealRedisConnection(redisConn);
                string lockValueFromRedis = actualConnection.StringGet(redisConn.Keys.LockKey);
                Assert.Null(lockValueFromRedis);

                // remove data from redis
                actualConnection.KeyDelete(redisConn.Keys.DataKey);
                DisposeRedisConnectionWrapper(redisConn);
            }
        }

        [Fact]
        public async Task TryReleaseLockIfLockIdMatch_ValidWriteLockRelease()
        {
            ProviderConfiguration pc = Utility.GetDefaultConfigUtility();
            using (RedisServer redisServer = new RedisServer())
            {
                RedisConnectionWrapper redisConn = GetRedisConnectionWrapperWithUniqueSession();

                // Inserting data into redis server
                SessionStateItemCollection data = new SessionStateItemCollection();
                data["key"] = "value";
                await redisConn.SetAsync(data, 900);

                const int lockTimeout = 900;

                DateTime lockTime = DateTime.Now;
                WriteLockData lockData = await redisConn.TryTakeWriteLockAndGetDataAsync(lockTime, lockTimeout);
                Assert.True(lockData.IsLockTaken);
                Assert.Equal(lockTime.Ticks.ToString(), lockData.LockId);
                Assert.Single(lockData.Data);

                await redisConn.TryReleaseLockIfLockIdMatchAsync(lockData.LockId, 900);

                // Get actual connection and check that lock do not exists
                IDatabase actualConnection = GetRealRedisConnection(redisConn);
                string lockValueFromRedis = actualConnection.StringGet(redisConn.Keys.LockKey);
                Assert.Null(lockValueFromRedis);

                // remove data from redis
                actualConnection.KeyDelete(redisConn.Keys.DataKey);
                DisposeRedisConnectionWrapper(redisConn);
            }
        }

        [Fact]
        public async Task TryReleaseLockIfLockIdMatch_InvalidWriteLockRelease()
        {
            ProviderConfiguration pc = Utility.GetDefaultConfigUtility();
            using (RedisServer redisServer = new RedisServer())
            {
                RedisConnectionWrapper redisConn = GetRedisConnectionWrapperWithUniqueSession();

                // Inserting data into redis server
                SessionStateItemCollection data = new SessionStateItemCollection();
                data["key"] = "value";
                await redisConn.SetAsync(data, 900);

                int lockTimeout = 900;

                DateTime lockTime = DateTime.Now;
                WriteLockData lockData = await redisConn.TryTakeWriteLockAndGetDataAsync(lockTime, lockTimeout);
                Assert.True(lockData.IsLockTaken);
                Assert.Equal(lockTime.Ticks.ToString(), lockData.LockId);
                Assert.Single(lockData.Data);

                object wrongLockId = lockTime.AddSeconds(1).Ticks.ToString();
                await redisConn.TryReleaseLockIfLockIdMatchAsync(wrongLockId, 900);

                // Get actual connection and check that lock do not exists
                IDatabase actualConnection = GetRealRedisConnection(redisConn);
                string lockValueFromRedis = actualConnection.StringGet(redisConn.Keys.LockKey);
                Assert.Equal(lockData.LockId, lockValueFromRedis);

                // remove data from redis
                actualConnection.KeyDelete(redisConn.Keys.DataKey);
                actualConnection.KeyDelete(redisConn.Keys.LockKey);
                DisposeRedisConnectionWrapper(redisConn);
            }
        }

        [Fact]
        public async Task TryRemoveIfLockIdMatch_ValidLockIdAndRemove()
        {
            ProviderConfiguration pc = Utility.GetDefaultConfigUtility();
            using (RedisServer redisServer = new RedisServer())
            {
                RedisConnectionWrapper redisConn = GetRedisConnectionWrapperWithUniqueSession();

                // Inserting data into redis server
                SessionStateItemCollection data = new SessionStateItemCollection();
                data["key"] = "value";
                await redisConn.SetAsync(data, 900);

                const int lockTimeout = 900;
                DateTime lockTime = DateTime.Now;
                WriteLockData lockData = await redisConn.TryTakeWriteLockAndGetDataAsync(lockTime, lockTimeout);
                Assert.True(lockData.IsLockTaken);
                Assert.Equal(lockTime.Ticks.ToString(), lockData.LockId);
                Assert.Single(lockData.Data);

                await redisConn.TryRemoveAndReleaseLockAsync(lockData.LockId);

                // Get actual connection and get data from redis
                IDatabase actualConnection = GetRealRedisConnection(redisConn);
                Assert.False(actualConnection.KeyExists(redisConn.Keys.DataKey));

                // check lock removed from redis
                Assert.False(actualConnection.KeyExists(redisConn.Keys.LockKey));
                DisposeRedisConnectionWrapper(redisConn);
            }
        }

        [Fact]
        public async Task TryUpdateIfLockIdMatch_WithValidUpdateAndDelete()
        {
            ProviderConfiguration pc = Utility.GetDefaultConfigUtility();
            using (RedisServer redisServer = new RedisServer())
            {
                RedisConnectionWrapper redisConn = GetRedisConnectionWrapperWithUniqueSession();

                // Inserting data into redis server
                SessionStateItemCollection data = new SessionStateItemCollection();
                data["key1"] = "value1";
                data["key2"] = "value2";
                data["key3"] = "value3";
                await redisConn.SetAsync(data, 900);

                const int lockTimeout = 900;
                DateTime lockTime = DateTime.Now;
                WriteLockData lockData = await redisConn.TryTakeWriteLockAndGetDataAsync(lockTime, lockTimeout);
                ISessionStateItemCollection dataFromRedis = lockData.Data;
                Assert.True(lockData.IsLockTaken);
                Assert.Equal(lockTime.Ticks.ToString(), lockData.LockId);
                Assert.Equal(3, dataFromRedis.Count);
                Assert.Equal("value1", dataFromRedis["key1"]);
                Assert.Equal("value2", dataFromRedis["key2"]);
                Assert.Equal("value3", dataFromRedis["key3"]);

                dataFromRedis["key2"] = "value2-updated";
                dataFromRedis.Remove("key3");
                await redisConn.TryUpdateAndReleaseLockAsync(lockData.LockId, dataFromRedis, 900);

                // Get actual connection and get data from redis
                IDatabase actualConnection = GetRealRedisConnection(redisConn);
                RedisValue sessionDataFromRedis = actualConnection.StringGet(redisConn.Keys.DataKey);
                SessionStateItemCollection dataFromRedis2 = null;
                MemoryStream ms = new MemoryStream(sessionDataFromRedis);
                BinaryReader reader = new BinaryReader(ms);
                dataFromRedis2 = SessionStateItemCollection.Deserialize(reader);

                Assert.Equal("value1", dataFromRedis2["key1"]);
                Assert.Equal("value2-updated", dataFromRedis2["key2"]);

                // check lock removed and remove data from redis
                actualConnection.KeyDelete(redisConn.Keys.DataKey);
                Assert.False(actualConnection.KeyExists(redisConn.Keys.LockKey));
                DisposeRedisConnectionWrapper(redisConn);
            }
        }

        [Fact]
        public async Task TryUpdateIfLockIdMatch_WithOnlyUpdateAndNoDelete()
        {
            ProviderConfiguration pc = Utility.GetDefaultConfigUtility();
            using (RedisServer redisServer = new RedisServer())
            {
                RedisConnectionWrapper redisConn = GetRedisConnectionWrapperWithUniqueSession();

                // Inserting data into redis server
                SessionStateItemCollection data = new SessionStateItemCollection();
                data["key1"] = "value1";
                data["key2"] = "value2";
                data["key3"] = "value3";
                await redisConn.SetAsync(data, 900);

                const int lockTimeout = 900;
                DateTime lockTime = DateTime.Now;
                WriteLockData lockData = await redisConn.TryTakeWriteLockAndGetDataAsync(lockTime, lockTimeout);
                Assert.True(lockData.IsLockTaken);
                Assert.Equal(lockTime.Ticks.ToString(), lockData.LockId);
                ISessionStateItemCollection dataFromRedis = lockData.Data;
                Assert.Equal(3, dataFromRedis.Count);
                Assert.Equal("value1", dataFromRedis["key1"]);
                Assert.Equal("value2", dataFromRedis["key2"]);
                Assert.Equal("value3", dataFromRedis["key3"]);

                dataFromRedis["key2"] = "value2-updated";
                await redisConn.TryUpdateAndReleaseLockAsync(lockData.LockId, dataFromRedis, 900);

                // Get actual connection and get data from redis
                IDatabase actualConnection = GetRealRedisConnection(redisConn);
                RedisValue sessionDataFromRedis = actualConnection.StringGet(redisConn.Keys.DataKey);
                SessionStateItemCollection sessionDataFromRedisAsCollection = null;
                MemoryStream ms = new MemoryStream(sessionDataFromRedis);
                BinaryReader reader = new BinaryReader(ms);
                sessionDataFromRedisAsCollection = SessionStateItemCollection.Deserialize(reader);

                Assert.Equal("value1", sessionDataFromRedisAsCollection["key1"]);
                Assert.Equal("value2-updated", sessionDataFromRedisAsCollection["key2"]);
                Assert.Equal("value3", sessionDataFromRedisAsCollection["key3"]);

                // check lock removed and remove data from redis
                actualConnection.KeyDelete(redisConn.Keys.DataKey);
                Assert.False(actualConnection.KeyExists(redisConn.Keys.LockKey));
                DisposeRedisConnectionWrapper(redisConn);
            }
        }

        [Fact]
        public async Task TryUpdateIfLockIdMatch_WithNoUpdateAndOnlyDelete()
        {
            ProviderConfiguration pc = Utility.GetDefaultConfigUtility();
            using (RedisServer redisServer = new RedisServer())
            {
                RedisConnectionWrapper redisConn = GetRedisConnectionWrapperWithUniqueSession();

                // Inserting data into redis server
                SessionStateItemCollection data = new SessionStateItemCollection();
                data["key1"] = "value1";
                data["key2"] = "value2";
                data["key3"] = "value3";
                await redisConn.SetAsync(data, 900);

                const int lockTimeout = 900;
                DateTime lockTime = DateTime.Now;
                WriteLockData lockData = await redisConn.TryTakeWriteLockAndGetDataAsync(lockTime, lockTimeout);
                Assert.True(lockData.IsLockTaken);
                Assert.Equal(lockTime.Ticks.ToString(), lockData.LockId);
                ISessionStateItemCollection dataFromRedis = lockData.Data;
                Assert.Equal(3, dataFromRedis.Count);
                Assert.Equal("value1", dataFromRedis["key1"]);
                Assert.Equal("value2", dataFromRedis["key2"]);
                Assert.Equal("value3", dataFromRedis["key3"]);

                dataFromRedis.Remove("key3");
                await redisConn.TryUpdateAndReleaseLockAsync(lockData.LockId, dataFromRedis, 900);

                // Get actual connection and get data from redis
                IDatabase actualConnection = GetRealRedisConnection(redisConn);
                RedisValue sessionDataFromRedis = actualConnection.StringGet(redisConn.Keys.DataKey);
                SessionStateItemCollection sessionDataFromRedisAsCollection = null;
                MemoryStream ms = new MemoryStream(sessionDataFromRedis);
                BinaryReader reader = new BinaryReader(ms);
                sessionDataFromRedisAsCollection = SessionStateItemCollection.Deserialize(reader);

                Assert.Equal("value1", sessionDataFromRedisAsCollection["key1"]);
                Assert.Equal("value2", sessionDataFromRedisAsCollection["key2"]);

                // check lock removed and remove data from redis
                actualConnection.KeyDelete(redisConn.Keys.DataKey);
                Assert.False(actualConnection.KeyExists(redisConn.Keys.LockKey));
                DisposeRedisConnectionWrapper(redisConn);
            }
        }

        [Fact]
        public async Task TryUpdateIfLockIdMatch_ExpiryTime_OnValidData()
        {
            ProviderConfiguration pc = Utility.GetDefaultConfigUtility();
            using (RedisServer redisServer = new RedisServer())
            {
                RedisConnectionWrapper redisConn = GetRedisConnectionWrapperWithUniqueSession();

                // Inserting data into redis server
                SessionStateItemCollection data = new SessionStateItemCollection();
                data["key"] = "value";
                data["key1"] = "value1";
                await redisConn.SetAsync(data, 900);

                // Check that data exists
                const int lockTimeout = 90;
                DateTime lockTime = DateTime.Now;
                WriteLockData lockData = await redisConn.TryTakeWriteLockAndGetDataAsync(lockTime, lockTimeout);
                ISessionStateItemCollection dataFromRedis = lockData.Data;
                Assert.Equal(2, dataFromRedis.Count);

                // Update expiry time to only 1 sec and then verify that.
                await redisConn.TryUpdateAndReleaseLockAsync(lockData.LockId, dataFromRedis, 1);

                // Wait for 1.1 seconds so that data will expire
                await Task.Delay(1100);

                // Get data blob from redis
                IDatabase actualConnection = GetRealRedisConnection(redisConn);
                HashEntry[] sessionDataFromRedisAfterExpire = actualConnection.HashGetAll(redisConn.Keys.DataKey);

                // Check that data is not there
                Assert.Empty(sessionDataFromRedisAfterExpire);
                DisposeRedisConnectionWrapper(redisConn);
            }
        }

        [Fact]
        public async Task TryUpdateAndReleaseLockIfLockIdMatch_LargeLockTime_ExpireManuallyTest()
        {
            ProviderConfiguration pc = Utility.GetDefaultConfigUtility();
            using (RedisServer redisServer = new RedisServer())
            {
                RedisConnectionWrapper redisConn = GetRedisConnectionWrapperWithUniqueSession();

                // Inserting data into redis server
                SessionStateItemCollection data = new SessionStateItemCollection();
                data["key1"] = "value1";
                await redisConn.SetAsync(data, 900);

                const int lockTimeout = 120000;
                DateTime lockTime = DateTime.Now;
                WriteLockData lockData = await redisConn.TryTakeWriteLockAndGetDataAsync(lockTime, lockTimeout);
                Assert.True(lockData.IsLockTaken);
                await redisConn.TryUpdateAndReleaseLockAsync(lockData.LockId, lockData.Data, 900);

                // Get actual connection and check that lock is released
                IDatabase actualConnection = GetRealRedisConnection(redisConn);
                Assert.False(actualConnection.KeyExists(redisConn.Keys.LockKey));
                actualConnection.KeyDelete(redisConn.Keys.DataKey);
                DisposeRedisConnectionWrapper(redisConn);
            }
        }

        [Fact]
        public async Task TryRemoveIfLockIdMatch_NullLockId()
        {
            ProviderConfiguration pc = Utility.GetDefaultConfigUtility();
            using (RedisServer redisServer = new RedisServer())
            {
                RedisConnectionWrapper redisConn = GetRedisConnectionWrapperWithUniqueSession();

                // Inserting data into redis server
                SessionStateItemCollection data = new SessionStateItemCollection();
                data["key"] = "value";
                await redisConn.SetAsync(data, 900);

                WriteLockData lockData = await redisConn.TryCheckWriteLockAndGetDataAsync();
                Assert.True(lockData.IsLockTaken);
                Assert.Null(lockData.LockId);
                Assert.Single(lockData.Data);

                await redisConn.TryRemoveAndReleaseLockAsync(null);

                // Get actual connection and get data from redis
                IDatabase actualConnection = GetRealRedisConnection(redisConn);
                Assert.False(actualConnection.KeyExists(redisConn.Keys.DataKey));

                // check lock removed from redis
                Assert.False(actualConnection.KeyExists(redisConn.Keys.LockKey));
                DisposeRedisConnectionWrapper(redisConn);
            }
        }

        [Fact]
        public async Task TryUpdateIfLockIdMatch_LockIdNull()
        {
            ProviderConfiguration pc = Utility.GetDefaultConfigUtility();
            using (RedisServer redisServer = new RedisServer())
            {
                RedisConnectionWrapper redisConn = GetRedisConnectionWrapperWithUniqueSession();

                // Inserting data into redis server
                SessionStateItemCollection data = new SessionStateItemCollection();
                data["key1"] = "value1";
                await redisConn.SetAsync(data, 900);

                WriteLockData lockData = await redisConn.TryCheckWriteLockAndGetDataAsync();
                ISessionStateItemCollection dataFromRedis = lockData.Data;
                Assert.True(lockData.IsLockTaken);
                Assert.Null(lockData.LockId);
                Assert.Single(dataFromRedis);

                // update session data without lock id (to support lock free session)
                dataFromRedis["key1"] = "value1-updated";
                await redisConn.TryUpdateAndReleaseLockAsync(null, dataFromRedis, 900);

                // Get actual connection and get data from redis
                IDatabase actualConnection = GetRealRedisConnection(redisConn);
                RedisValue sessionDataFromRedis = actualConnection.StringGet(redisConn.Keys.DataKey);
                SessionStateItemCollection sessionDataFromRedisAsCollection = null;
                MemoryStream ms = new MemoryStream(sessionDataFromRedis);
                BinaryReader reader = new BinaryReader(ms);
                sessionDataFromRedisAsCollection = SessionStateItemCollection.Deserialize(reader);

                Assert.Equal("value1-updated", sessionDataFromRedisAsCollection["key1"]);

                // check lock removed and remove data from redis
                actualConnection.KeyDelete(redisConn.Keys.DataKey);
                Assert.False(actualConnection.KeyExists(redisConn.Keys.LockKey));
                DisposeRedisConnectionWrapper(redisConn);
            }
        }

        private IDatabase GetRealRedisConnection(RedisConnectionWrapper redisConn)
        {
            return (IDatabase)((StackExchangeClientConnection)redisConn.redisConnection).RealConnection;
        }
    }
}
