using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Orchard.Mvc.Routes;
using System.Web.Routing;
using System.Web.Mvc;

namespace Orchard.ContentTree
{
    public class Routes : IRouteProvider
    {
        public void GetRoutes(ICollection<RouteDescriptor> routes)
        {
            foreach (var routeDescriptor in GetRoutes())
            {
                routes.Add(routeDescriptor);
            }
        }

        public IEnumerable<RouteDescriptor> GetRoutes()
        {
            string areaName = "Orchard.ContentTree";

            var emptyConstraints = new RouteValueDictionary();
            var sliderRouteValueDictionary = new RouteValueDictionary { { "area", areaName } };
            var mvcRouteHandler = new MvcRouteHandler();

            return new[] {
                new RouteDescriptor {
                    Priority = 0,
                    Route = new Route(
                        "Admin/Contents/ContentTree",
                        new RouteValueDictionary {
                            {"area", areaName},
                            {"controller", "Admin"},
                            {"action", "ContentTree"},
                        },
                        emptyConstraints, sliderRouteValueDictionary, mvcRouteHandler)
                },
                new RouteDescriptor {
                    Priority = 0,
                    Route = new Route(
                        "Admin/Contents/ContentTreeAsync",
                        new RouteValueDictionary {
                            {"area", areaName},
                            {"controller", "Admin"},
                            {"action", "ContentTreeAsync"},
                        },
                        emptyConstraints, sliderRouteValueDictionary, mvcRouteHandler)
                },
                new RouteDescriptor {
                    Route = new Route(
                        "Admin/Contents/ContentTree/SaveActions",
                        new RouteValueDictionary {
                            {"area", areaName},
                            {"controller", "Admin"},
                            {"action", "SaveActions"},
                        },
                        emptyConstraints, sliderRouteValueDictionary, mvcRouteHandler)
                }
            };
        }
    }
}