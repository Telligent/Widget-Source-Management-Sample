using System;
using System.IO;
using System.Text;
using Telligent.Evolution.Extensibility.Api.Entities.Version1;
using Telligent.Evolution.Extensibility.Storage.Version1;

namespace Samples.Model
{
    public abstract class InstallableFile : IInstallableFile
    {
        public string ResourcePath
        {
            get; set;
        }

        public Func<Stream> Content
        {
            get
            {
                return () =>
                {
                    var name = new StringBuilder(GetType().Assembly.GetName().Name);
                    var components = ResourcePath.Split(System.IO.Path.DirectorySeparatorChar);
                    int r;
                    for (int i = 0; i < components.Length; i++)
                    {
                        // folders are seperated with a period
                        name.Append(".");

                        // numeric paths are prefixed with an underbar
                        if (i < components.Length - 1 && int.TryParse(components[i].Substring(0, 1), out r))
                            name.Append("_");

                        name.Append(components[i]);
                    }

                    return GetType().Assembly.GetManifestResourceStream(name.ToString());
                };
            }
            set { }
        }

        string _cfsPath = null;
        public string CfsPath
        {
            get
            {
                if (_cfsPath == null)
                {
                    var path = WidgetProviderId.ToString("N");

                    if (WidgetId != Guid.Empty)
                        path = CentralizedFileStorage.MakePath(path, WidgetId.ToString("N"));

                    if (ThemeId.HasValue)
                        path = CentralizedFileStorage.MakePath(path, ThemeId.Value.ToString("N"));

                    _cfsPath = path;
                }

                return _cfsPath;
            }
            set { }
        }

        public string FileName
        {
            get; set;
        }

        public Version LastModifiedVersion
        {
            get; set;
        }

        public Guid WidgetProviderId
        {
            get; set;
        }

        public Guid WidgetId
        {
            get; set;
        }

        public Guid? ThemeId
        {
            get; set;
        }
    }
}
