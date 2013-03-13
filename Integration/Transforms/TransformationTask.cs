using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using Microsoft.Web.Publishing.Tasks;
using Octopus.Shared.Activities;

namespace Octopus.Shared.Integration.Transforms
{
    /// <summary>
    /// Make transformation of file <see cref="SourceFilePath"/> with transform file <see cref="TransformFile"/>.
    /// Look at http://msdn.microsoft.com/en-us/library/dd465326.aspx for syntax of transformation file.
    /// </summary>
    public class TransformationTask
    {
        readonly IActivityLog log;
        readonly TransformationLogger transformationLogger;
        IDictionary<string, string> parameters;

        /// <summary>
        /// Create new TransformationTask object and set values for <see cref="SourceFilePath"/> and <see cref="TransformFile"/>
        /// </summary>
        /// <param name="sourceFilePath">Source file path</param>
        /// <param name="transformFilePath">Transformation file path</param>
        /// <param name="log"> </param>
        public TransformationTask(string sourceFilePath, string transformFilePath, IActivityLog log)
        {
            this.log = log;
            transformationLogger = new TransformationLogger(log);

            SourceFilePath = sourceFilePath;
            TransformFile = transformFilePath;
        }

        /// <summary>
        /// Set parameters and values for transform
        /// </summary>
        /// <param name="parameters">Dictionary of parameters with values.</param>
        public void SetParameters(IDictionary<string, string> parameters)
        {
            this.parameters = parameters;
        }

        /// <summary>
        /// Source file
        /// </summary>
        public string SourceFilePath { get; set; }

        /// <summary>
        /// Transformation file
        /// </summary>
        /// <remarks>
        /// See http://msdn.microsoft.com/en-us/library/dd465326.aspx for syntax of transformation file
        /// </remarks>
        public string TransformFile { get; set; }

        /// <summary>
        /// Make transformation of file <see cref="SourceFilePath"/> with transform file <see cref="TransformFile"/> to <paramref name="destinationFilePath"/>.
        /// </summary>
        /// <param name="destinationFilePath">File path of destination transformation.</param>
        /// <param name="forceParametersTask">Invoke parameters task even if the parameters are not set with <see cref="SetParameters" />.</param>
        /// <returns>Return true if transformation finish successfully, otherwise false.</returns>
        public TransformResult Execute(string destinationFilePath, bool forceParametersTask = false)
        {
            if (string.IsNullOrWhiteSpace(destinationFilePath))
                throw new ArgumentException("Destination file can't be empty.", "destinationFilePath");

            log.DebugFormat("Start tranformation to '{0}'.", destinationFilePath);

            if (string.IsNullOrWhiteSpace(SourceFilePath) || !File.Exists(SourceFilePath))
                throw new FileNotFoundException("Can't find source file.", SourceFilePath);

            if (string.IsNullOrWhiteSpace(TransformFile) || !File.Exists(TransformFile))
                throw new FileNotFoundException("Can't find transform  file.", TransformFile);

            log.DebugFormat("Source file: '{0}'.", SourceFilePath);
            log.DebugFormat("Transform  file: '{0}'.", TransformFile);

            try
            {
                var transformFile = ReadContent(TransformFile);

                if ((parameters != null && parameters.Count > 0) || forceParametersTask)
                {
                    var parametersTask = new ParametersTask();
                    if (parameters != null)
                        parametersTask.AddParameters(parameters);
                    transformFile = parametersTask.ApplyParameters(transformFile);
                }

                var document = new XmlDocument();
                document.Load(SourceFilePath);

                var transformation = new XmlTransformation(transformFile, false, transformationLogger);

                transformation.Apply(document);

                document.Save(destinationFilePath);

                if (transformationLogger.WasErrorLogged)
                {
                    return TransformResult.SuccessWithErrors;
                }

                if (transformationLogger.WasWarningLogged)
                {
                    return TransformResult.SuccessWithWarnings;
                }

                return TransformResult.Success;
            }
            catch (Exception e)
            {
                log.Error(e);
                return TransformResult.Failed;
            }
        }

        static string ReadContent(string path)
        {
            using (var reader = new StreamReader(path))
            {
                return reader.ReadToEnd();
            }
        }
    }

    public enum TransformResult
    {
        Success,
        SuccessWithErrors,
        SuccessWithWarnings,
        Failed
    }
}