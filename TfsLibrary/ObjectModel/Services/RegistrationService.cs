using System;
using System.Collections.Generic;
using System.Net;
using CodePlex.TfsLibrary.RegistrationWebSvc;

namespace CodePlex.TfsLibrary.ObjectModel
{
    public class RegistrationService : IRegistrationService
    {
        static readonly Dictionary<string, FrameworkRegistrationEntry[]> entriesCache = new Dictionary<string, FrameworkRegistrationEntry[]>();
        readonly IRegistrationWebSvcFactory registrationWebSvcFactory;

        public RegistrationService(IRegistrationWebSvcFactory registrationWebSvcFactory)
        {
            this.registrationWebSvcFactory = registrationWebSvcFactory;
        }

        public string GetServiceInterfaceUrl(string tfsUrl,
                                             ICredentials credentials,
                                             string serviceType,
                                             string interfaceName)
        {
            FrameworkRegistrationEntry[] entries;

            lock (entriesCache)
            {
                string lowerTfsUrl = tfsUrl.ToLowerInvariant();

                if (!entriesCache.TryGetValue(lowerTfsUrl, out entries))
                {
                    using (Registration registrationWebSvc = (Registration)registrationWebSvcFactory.Create(tfsUrl, credentials))
                    {
                        if (tfsUrl.EndsWith("/"))
                            tfsUrl = tfsUrl.Substring(0, tfsUrl.Length - 1);

                        try
                        {
                            entries = registrationWebSvc.GetRegistrationEntries(null);
                            entriesCache[lowerTfsUrl] = entries;
                        }
                        catch (WebException ex)
                        {
                            HttpWebResponse response = ex.Response as HttpWebResponse;

                            if (response == null || response.StatusCode != HttpStatusCode.Unauthorized)
                                throw;

                            throw new NetworkAccessDeniedException(ex);
                        }
                        catch (Exception ex)
                        {
                            if (ex.Message.Contains("TF50309:") ||
                                ex.Message.Contains("TF10158:"))
                                throw new NetworkAccessDeniedException(ex);
                            else
                                throw;
                        }
                    }
                }
            }

            foreach (FrameworkRegistrationEntry entry in entries)
            {
                if (string.Compare(entry.Type, serviceType, true) == 0)
                {
                    foreach (RegistrationServiceInterface iface in entry.ServiceInterfaces)
                        if (string.Compare(iface.Name, interfaceName, true) == 0)
                            return tfsUrl + iface.Url;

                    throw new ArgumentException("Unknown interface name " + interfaceName + " for service type " + serviceType, "interfaceName");
                }
            }

            throw new ArgumentException("Unknown service type " + serviceType, "serviceType");
        }
    }
}