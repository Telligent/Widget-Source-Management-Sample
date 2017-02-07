using System;
using System.Collections.Generic;
using System.Linq;
using Telligent.Evolution.Extensibility.Version1;
using Telligent.Evolution.Extensibility.UI.Version1;
using Telligent.Evolution.Extensibility.Storage.Version1;
using Telligent.Evolution.Extensibility;
using Telligent.Evolution.Extensibility.Api.Version1;
using Telligent.Evolution.Extensibility.Api.Entities.Version1;
using Samples.Generated;

namespace Samples
{
    public class FactoryDefaultWidgetProvider : IPlugin, IScriptedContentFragmentFactoryDefaultProvider, IInstallablePlugin
    {
        // the provider ID should be updated to be unique (with Developer Mode enabled, go to Administration > Development >  Generate GUID to get a new GUID)
        private readonly Guid _providerId = new Guid("fa801aba-84a0-4746-92cc-b418a7106c0b");

        private Version _emptyVersion = new Version(0, 0, 0, 0);
#if DEBUG
        private Version _version = new Version(1, 0, 0, 0);
#else
        private Version _version = null;
#endif

        #region IPlugin Implementation

        public string Name
        {
            get
            {
                return "Sample Widgets";
            }
        }

        public string Description
        {
            get
            {
                return "A sample collection of factory default widgets.";
            }
        }

        public void Initialize()
        {
        }

        #endregion

        #region IScriptedContentFragmentFactoryDefaultProvider

        public Guid ScriptedContentFragmentFactoryDefaultIdentifier
        {
            get { return _providerId; }
        }

        #endregion

        #region IInstallablePlugin Implementation

        public Version Version
        {
            get
            {
                if (_version == null)
                {
                    Version version = _emptyVersion;

                    foreach(var file in FactoryDefaultWidgetFileManifest.Files().Where(x => x.WidgetProviderId == ScriptedContentFragmentFactoryDefaultIdentifier))
                    {
                        if (file.LastModifiedVersion > version)
                            version = file.LastModifiedVersion;
                    }

                    _version = version;
                }

                return _version;
            }
        }

        public void Install(Version lastInstalledVersion)
        {
            if (Version == _emptyVersion)
                return;

            var files = FactoryDefaultWidgetFileManifest.Files().Where(x => x.WidgetProviderId == ScriptedContentFragmentFactoryDefaultIdentifier).ToList();

            DetectedDeletedProviderFiles(files);

            // detect upgrade changes and version widgets appropriately for post-upgrade review (or just install all of the files if this isn't an upgrade)
            var message = ContentFragments.UpdateScriptedContentFragments(
                lastInstalledVersion,
                files.Select(x => x as IInstallableFile).ToList()
                );

            // if this was an upgrade, identify the upgrade as complete and provide a way to review widgets (if any were modified as part of the upgrade)
            if (lastInstalledVersion > _emptyVersion)
            {
                // identify that the plugin was upgraded and provide the note to review widget changes, if there was one
                Apis.Get<ISystemNotifications>().Create(
                    string.Concat(Name, " Upgraded"),
                    string.Concat("<p>", Name, " has been upgraded to ", Version.ToString(), ".</p>", message ?? "")
                    );
            }
        }

        public void Uninstall()
        {
            if (Version == _emptyVersion)
                return;

            FactoryDefaultScriptedContentFragmentProviderFiles.DeleteAllFiles(this);
        }

        #endregion

        void DetectedDeletedProviderFiles(List<Model.InstallableFile> files)
        {
            Guid factoryDefaultProviderId, widgetId, themeId;
            foreach (var file in CentralizedFileStorage.GetFileStore("defaultwidgets").GetFiles(ScriptedContentFragmentFactoryDefaultIdentifier.ToString("N"), PathSearchOption.AllPaths))
            {
                if (!files.Any(x => x.CfsPath == file.Path && x.FileName == file.FileName))
                {
                    var pathComponents = file.Path.Split(new char[] { CentralizedFileStorage.DirectorySeparator }, StringSplitOptions.RemoveEmptyEntries);

                    if (pathComponents.Length == 1 && Guid.TryParse(pathComponents[0], out factoryDefaultProviderId))
                    {
                        files.Add(new Model.DeletedWidgetDefinitionFile
                        {
                            FileName = file.FileName,
                            LastModifiedVersion = Version,
                            WidgetProviderId = ScriptedContentFragmentFactoryDefaultIdentifier
                        });
                    }
                    else if (pathComponents.Length == 2 && Guid.TryParse(pathComponents[0], out factoryDefaultProviderId) && Guid.TryParse(pathComponents[1], out widgetId))
                    {
                        files.Add(new Model.DeletedSupplementaryFile
                        {
                            FileName = file.FileName,
                            LastModifiedVersion = Version,
                            WidgetProviderId = ScriptedContentFragmentFactoryDefaultIdentifier,
                            WidgetId = widgetId
                        });
                    }
                    else if (pathComponents.Length == 3 && Guid.TryParse(pathComponents[0], out factoryDefaultProviderId) && Guid.TryParse(pathComponents[1], out widgetId) && Guid.TryParse(pathComponents[2], out themeId))
                    {
                        files.Add(new Model.DeletedSupplementaryFile
                        {
                            FileName = file.FileName,
                            LastModifiedVersion = Version,
                            WidgetProviderId = ScriptedContentFragmentFactoryDefaultIdentifier,
                            WidgetId = widgetId,
                            ThemeId = themeId
                        });
                    }
                }
            }
        }
    }
}
