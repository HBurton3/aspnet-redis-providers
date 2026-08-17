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
    internal interface IRedisClientConnection
    {
        object Eval(string script, string[] keyArgs, object[] valueArgs);
        Task<object> EvalAsync(string script, string[] keyArgs, object[] valueArgs, CancellationToken token = default);
        string GetLockId(object rowDataFromRedis);
        int GetSessionTimeout(object rowDataFromRedis);
        bool IsLocked(object rowDataFromRedis);
        ISessionStateItemCollection GetSessionData(object rowDataFromRedis);
        void Set(string key, byte[] data, DateTime utcExpiry);
        byte[] Get(string key);
        void Remove(string key);
    }
}
