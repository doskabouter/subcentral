﻿using System;
using System.IO;
using NLog;

namespace SubCentral.Utils
{
    public sealed class NetUtils
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public static bool uncHostIsAlive(string path)
        {
            if (string.IsNullOrEmpty(path)) return true;

            if (!new Uri(path).IsUnc)
            {
                DriveInfo di = FileUtils.GetDriveInfoFromPath(path);
                if (di != null && di.DriveType == DriveType.Network)
                {
                    path = MPR.GetUniversalName(path);
                }
                else
                    return true;
            }

            Uri uri = new Uri(path);
            string hostName = uri.Host;
            bool alive = isMachineReachable(hostName);

            return alive;
        }

        public static bool isMachineReachable(string hostName)
        {
            try
            {
                System.Net.IPHostEntry host = System.Net.Dns.GetHostEntry(hostName);
                //System.Net.IPHostEntry host = System.Net.Dns.Resolve(hostName);
                //System.Net.IPHostEntry host = System.Net.Dns.GetHostByName(hostName);
                //System.Net.IPHostEntry host = null;

                //return true; 

                string wqlTemplate = "SELECT StatusCode FROM Win32_PingStatus WHERE Address = '{0}'";

                System.Management.ManagementObjectSearcher query = new System.Management.ManagementObjectSearcher();

                query.Query = new System.Management.ObjectQuery(String.Format(wqlTemplate, host.AddressList[0]));

                query.Scope = new System.Management.ManagementScope("//localhost/root/cimv2");

                System.Management.ManagementObjectCollection pings = query.Get();

                foreach (System.Management.ManagementObject ping in pings)
                {
                    if (Convert.ToInt32(ping.GetPropertyValue("StatusCode")) == 0)
                        return true;
                }
            }
            catch (Exception e)
            {
                logger.Error("Error in isMachineReachable({0}): {1}:{2}", hostName, e.GetType(), e.Message);
            }

            return false;
        }
    }
}