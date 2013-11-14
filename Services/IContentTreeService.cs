using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Mvc;
using Orchard.Autoroute.Models;
using Orchard.ContentManagement;
using Orchard.ContentTree.Models;
using Orchard.ContentTree.Security;
using Orchard.Core.Title.Models;
using Orchard.DisplayManagement;
using Orchard.Settings;

namespace Orchard.ContentTree.Services {
    public interface IContentTreeService : IDependency {
        ContentTreeItem BuildTree();
        ContentTreeItem GetTreeItem(ContentItem item);
        dynamic Display(ContentTreeItem tree);
        string Render(ControllerContext context, TempDataDictionary temp, string viewName, object model);
        ContentTreeSettingsPart GetSettings();
        IEnumerable<SelectListItem> ContentTypeOptions(string[] selectedTypes);
    }

    public class ContentTreeService : IContentTreeService {
        private readonly IContentManager _contentManager;
        private readonly ISite _siteSettings;
        private readonly IEnumerable<ITreePermissionProvider> _treePermissionProviders;
        private dynamic _shapeFactory;

        public ContentTreeService(
            IContentManager contentManager, 
            ISiteService siteService, 
            IShapeFactory shapeFactory, 
            IEnumerable<ITreePermissionProvider> treePermissionProviders) 
        {
            _contentManager = contentManager;
            _siteSettings = siteService.GetSiteSettings();
            _treePermissionProviders = treePermissionProviders;
            _shapeFactory = shapeFactory;
        }

        public ContentTreeItem BuildTree() {
            //Get all routed content, of the types selected in settings.
            var content = _contentManager.
                Query<AutoroutePart, AutoroutePartRecord>(GetSettings().IncludedTypes).
                ForVersion(VersionOptions.Latest).
                Where(a => a.DisplayAlias != null && a.DisplayAlias != "").
                WithQueryHints(new QueryHints().ExpandParts<TitlePart>()).
                List();

            //Special case, get the homepage.
            var root = GetHomepageTreeItem();

            if (root == null) {
                return null;
            }

            //Create a flat tree, and add placeholder items where required in order to preserve structure.
            var flat = new Dictionary<string, ContentTreeItem>();

            foreach (var item in content.OrderBy(i => i.DisplayAlias)) {
                var branch = item.ToTreeItem();

                if (branch.Level == 1) {
                    flat.Add(branch.Path, branch);
                }
                else {
                    var parts = branch.Path.Split('/');

                    for (int i = 1; i < branch.Level; i++) {
                        var ancestorPath = String.Join("/", parts.Take(i));

                        if (!flat.ContainsKey(ancestorPath)) {
                            flat.Add(ancestorPath, CreatePlaceholder(i, ancestorPath));
                        }
                    }

                    if (!flat.ContainsKey(branch.Path)) {
                        flat.Add(branch.Path, branch);
                    }
                }
            }

            //Hierarchize the tree.
            foreach (var item in flat.Values) {
                if (item.Level == 1) {
                    root.Children.Add(item);
                }
                else {
                    var ancestor = root.Children.LastOrDefault(c => item.Path.StartsWith(c.Path));
                        
                    var parent = ancestor;

                    while (ancestor != null) {
                        parent = ancestor;

                        ancestor = ancestor.Children.LastOrDefault(c => item.Path.StartsWith(c.Path));
                    }

                    parent.Children.Add(item);
                }
            }

            return root;
        }

        public dynamic Display(ContentTreeItem tree)
        {
            List<dynamic> children = new List<dynamic>();

            if (tree.Children.Count > 0) {
                foreach (var child in tree.Children) {
                    children.Add(Display(child));
                }
            }

            dynamic shape;

            if (tree.Content == null) {
                shape = _shapeFactory.Parts_TreePlaceholder(
                    Title: String.Format("{0}", tree.Title),
                    Path: tree.Path,
                    Level: tree.Level,
                    Editable: false
                );
            } 
            else {
                shape = _shapeFactory.Parts_TreeItem(
                    Content: tree.Content,
                    Title: String.Format("{0} ({1})", tree.Title, tree.Content.ContentItem.ContentType),
                    Path: tree.Path,
                    Level: tree.Level,
                    Editable: _treePermissionProviders.All(p => p.Editable(tree.Content)),
                    Actions: _shapeFactory.Parts_TreeItemActions(ContentPart: tree.Content, HasChildren: tree.Children.Count > 0)
                );
            }

            shape.NoHide = shape.Editable || children.Any(c => c.Editable || c.NoHide);
            shape.Children = children;

            return shape;
        }

        public string Render(ControllerContext context, TempDataDictionary temp, string viewName, object model) {
            var engine = CreatePartViewEngine();

            var viewResult = engine.FindPartialView(context, viewName, false);

            if (viewResult.View == null) {
                
                throw new Exception("Couldn't find view " + viewName);
            }

            var viewData = new ViewDataDictionary { Model = model };

            using (var writer = new StringWriter()) {
                var viewContext = new ViewContext(context, viewResult.View, viewData, temp, writer);
                
                viewResult.View.Render(viewContext, writer);

                return writer.GetStringBuilder().ToString();
            }
        }

        public ContentTreeItem BuildSingleItem(int id) {
            var content = _contentManager.Get(id, VersionOptions.Latest);

            return GetTreeItem(content);
        }

        public ContentTreeItem GetTreeItem(ContentItem item) {
            var route = item.As<AutoroutePart>();

            return route == null ? null : route.ToTreeItem();
        }

        public ContentTreeSettingsPart GetSettings() {
            var settings = _contentManager.Query<ContentTreeSettingsPart, ContentTreeSettingsRecord>("ContentTreeSettings").List().FirstOrDefault();

            if (settings == null)
            {
                settings = _contentManager.Create<ContentTreeSettingsPart>("ContentTreeSettings", item =>
                {
                    item.IncludedTypes = new string[] { "Page" };
                });
            }

            return settings;
        }

        public IEnumerable<SelectListItem> ContentTypeOptions(string[] selectedTypes) {
            foreach (var type in _contentManager.GetContentTypeDefinitions())
            {
                if (type.Parts.Any(p => p.PartDefinition.Name.Equals("AutoroutePart")))
                {
                    yield return new SelectListItem
                    {
                        Text = type.DisplayName,
                        Value = type.Name,
                        Selected = selectedTypes.Contains(type.Name)
                    };
                }
            }
        }

        #region Private Utility Methods

        private ContentTreeItem GetHomepageTreeItem() {
            var homepage = _contentManager.Query<AutoroutePart, AutoroutePartRecord>().Where<AutoroutePartRecord>(r => r.DisplayAlias.Equals(String.Empty)).Slice(0, 1).FirstOrDefault();

            if (homepage == null) {
                throw new InvalidOperationException("Homepage content item was not found.");
            }

            return new ContentTreeItem
            {
                Content = homepage,
                Path = "",
                Name = "",
                Title = homepage.As<TitlePart>().Title,
                Children = new List<ContentTreeItem>(),
            };
        }

        private ContentTreeItem CreatePlaceholder(int level, string path) {
            return new ContentTreeItem
            {
                Content = null,
                Path = path,
                Name = path.Split('/').Last(),
                Title = path.Split('/').Last(),
                Level = level,
                Children = new List<ContentTreeItem>()
            };
        }

        //CD: Feels like this isn't the right 'Orchard' way to do this, but it works. Would welcome input on a more correct way.
        private RazorViewEngine CreatePartViewEngine() {
            string[] areaFormats = new string[] 
            { 
                "~/Modules/{2}/Views/Parts/{0}.cshtml"
            };

            return new RazorViewEngine {
                MasterLocationFormats = new string[0],
                ViewLocationFormats = new string[0],
                PartialViewLocationFormats = new string[0],
                AreaMasterLocationFormats = new string[0],
                AreaViewLocationFormats = areaFormats,
                AreaPartialViewLocationFormats = areaFormats,
            };
        }

        #endregion
    }

    public static class ContentTreeExtensions {
        public static ContentTreeItem ToTreeItem(this AutoroutePart content) {
            return new ContentTreeItem
            {
                Content = content,
                Path = content.DisplayAlias.ToLowerInvariant(),
                Title = content.As<TitlePart>().Title,
                Name = content.DisplayAlias.Split('/').Last(),
                Level = content.DisplayAlias.Split('/').Length,
                Children = new List<ContentTreeItem>()
            };
        }
    }
}