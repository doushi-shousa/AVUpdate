﻿using System;
using System.ComponentModel;
using System.Net;
using System.Runtime.InteropServices;

namespace AVUpdate
{
    public class NetworkConnection : IDisposable
    {
        private string _networkName;

        public NetworkConnection(string networkName, NetworkCredential credentials)
        {
            _networkName = networkName;

            var netResource = new NetResource
            {
                Scope = ResourceScope.GlobalNetwork,
                ResourceType = ResourceType.Disk,
                DisplayType = ResourceDisplayType.Share,
                RemoteName = networkName
            };

            // Формируем имя пользователя. Если нужен домен – укажите его в формате "DOMAIN\username".
            string userName = credentials.UserName;
            int result = WNetAddConnection2(netResource, credentials.Password, userName, 0);

            if (result != 0)
            {
                throw new Win32Exception(result, "Не удалось подключиться к сетевому ресурсу: " + networkName);
            }
        }

        ~NetworkConnection()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            WNetCancelConnection2(_networkName, 0, true);
        }

        [DllImport("mpr.dll")]
        private static extern int WNetAddConnection2(NetResource netResource, string password, string username, int flags);

        [DllImport("mpr.dll")]
        private static extern int WNetCancelConnection2(string name, int flags, bool force);
    }

    [StructLayout(LayoutKind.Sequential)]
    public class NetResource
    {
        public ResourceScope Scope;
        public ResourceType ResourceType;
        public ResourceDisplayType DisplayType;
        public int Usage;
        public string LocalName;
        public string RemoteName;
        public string Comment;
        public string Provider;
    }

    public enum ResourceScope : int
    {
        Connected = 1,
        GlobalNetwork,
        Remembered,
        Recent,
        Context
    };

    public enum ResourceType : int
    {
        Any = 0,
        Disk = 1,
        Print = 2,
        Reserved = 8,
    }

    public enum ResourceDisplayType : int
    {
        Generic = 0,
        Domain = 1,
        Server = 2,
        Share = 3,
        File = 4,
        Group = 5,
        Network = 6,
        Root = 7,
        Shareadmin = 8,
        Directory = 9,
        Tree = 10,
        Ndscontainer = 11
    }
}
