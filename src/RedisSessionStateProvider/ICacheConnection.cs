//
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.
//

using System;
using System.Threading;
using System.Threading.Tasks;
using System.Web.SessionState;

namespace Microsoft.Web.Redis
{
    internal struct WriteLockData
    {
        public bool IsLockTaken { get; }
        public object LockId { get; }
        public ISessionStateItemCollection Data { get; }
        public int SessionTimeout { get; }

        public WriteLockData(bool isLockTaken, object lockId, ISessionStateItemCollection data, int sessionTimeout)
        {
            IsLockTaken = isLockTaken;
            LockId = lockId;
            Data = data;
            SessionTimeout = sessionTimeout;
        }
    }

    internal interface ICacheConnection
    {
        KeyGenerator Keys { get; set; }
        Task SetAsync(ISessionStateItemCollection data, int sessionTimeout, CancellationToken token = default);
        Task UpdateExpiryTimeAsync(int timeToExpireInSeconds, CancellationToken token = default);
        Task<WriteLockData> TryTakeWriteLockAndGetDataAsync(DateTime lockTime, int lockTimeout, CancellationToken token = default);
        Task<WriteLockData> TryCheckWriteLockAndGetDataAsync(CancellationToken token = default);
        Task TryReleaseLockIfLockIdMatchAsync(object lockId, int sessionTimeout, CancellationToken token = default);
        Task TryRemoveAndReleaseLockAsync(object lockId, CancellationToken token = default);
        Task TryUpdateAndReleaseLockAsync(object lockId, ISessionStateItemCollection data, int sessionTimeout, CancellationToken token = default);
        TimeSpan GetLockAge(object lockId);
    }
}
