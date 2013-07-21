using System;

namespace Octopus.Shared.Integration.Scripting
{
    public class CreatedArtifact
    {
        readonly string path;
        readonly string originalFilename;

        public CreatedArtifact(string path, string originalFilename)
        {
            if (path == null) throw new ArgumentNullException("path");
            if (originalFilename == null) throw new ArgumentNullException("originalFilename");
            this.path = path;
            this.originalFilename = originalFilename;
        }

        public string Path
        {
            get { return path; }
        }

        public string OriginalFilename
        {
            get { return originalFilename; }
        }
    }
}