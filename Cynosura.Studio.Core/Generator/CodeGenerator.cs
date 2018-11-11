﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Cynosura.Studio.Core.Generator.Models;
using Cynosura.Studio.Core.Merge;
using Cynosura.Studio.Core.PackageFeed;
using Cynosura.Studio.Core.TemplateEngine;
using Microsoft.Extensions.Logging;

namespace Cynosura.Studio.Core.Generator
{
    public class CodeGenerator
    {
        private const string PackageName = "Cynosura.Template";
        private string StudioDirectoryPath => Path.Combine(Path.GetTempPath(), "Cynosura.Studio");

        private readonly ITemplateEngine _templateEngine;
        private readonly IPackageFeed _packageFeed;
        private readonly IMerge _merge;
        private readonly FileMerge _fileMerge;
        private readonly ILogger<CodeGenerator> _logger;

        public CodeGenerator(ITemplateEngine templateEngine, 
            IPackageFeed packageFeed,
            IMerge merge,
            FileMerge fileMerge,
            ILogger<CodeGenerator> logger)
        {
            _templateEngine = templateEngine;
            _packageFeed = packageFeed;
            _merge = merge;
            _fileMerge = fileMerge;
            _logger = logger;
        }

        private string ProcessTemplate(CodeTemplate template, SolutionAccessor solutionAccessor, object model)
        {
            var templatePath = solutionAccessor.GetTemplatePath(template);
            return _templateEngine.ProcessTemplate(templatePath, model);
        }

        private string GetTemplateFilePath(CodeTemplate template, SolutionAccessor solution, ISimpleTemplateProcessor fileNameTemplateProcessor)
        {
            var dir = FindDirectory(solution.Path, template.FilePath);
            var fileName = fileNameTemplateProcessor.ProcessTemplate(template.FileName);
            var filePath = Path.Combine(dir, fileName);
            var fileDirectory = Path.GetDirectoryName(filePath);
            if (!Directory.Exists(fileDirectory))
                Directory.CreateDirectory(fileDirectory);
            return filePath;
        }

        private async Task CreateFileAsync(CodeTemplate template, object model, SolutionAccessor solution, ISimpleTemplateProcessor fileNameTemplateProcessor)
        {
            var filePath = GetTemplateFilePath(template, solution, fileNameTemplateProcessor);
            var content = ProcessTemplate(template, solution, model);

            if (!string.IsNullOrEmpty(template.InsertAfter))
            {
                var fileContent = await ReadFileAsync(filePath);

                if (!fileContent.Contains(content))
                {
                    fileContent = fileContent.Replace(template.InsertAfter + Environment.NewLine,
                        template.InsertAfter + Environment.NewLine + content + Environment.NewLine);

                    await WriteFileAsync(filePath, fileContent);
                }
            }
            else
            {
                await WriteFileAsync(filePath, content);
            }
        }

        private async Task UpgradeFileAsync(CodeTemplate template, object oldModel, object newModel, SolutionAccessor solution, ISimpleTemplateProcessor oldFileNameTemplateProcessor, ISimpleTemplateProcessor newFileNameTemplateProcessor)
        {
            var oldContent = ProcessTemplate(template, solution, oldModel);
            var newContent = ProcessTemplate(template, solution, newModel);
            var oldFilePath = GetTemplateFilePath(template, solution, oldFileNameTemplateProcessor);
            var newFilePath = GetTemplateFilePath(template, solution, newFileNameTemplateProcessor);
            if (!File.Exists(oldFilePath))
            {
                _logger.LogWarning($"File {oldFilePath} is not found. Skip upgrade");
                return;
            }
            var oldFileContent = await ReadFileAsync(oldFilePath);
            var newFileContent = _merge.Merge(oldContent, newContent, oldFileContent);
            if (oldFilePath != newFilePath)
            {
                File.Delete(oldFilePath);
            }
            if (oldFileContent == newFileContent && oldFilePath == newFilePath)
                return;
            await WriteFileAsync(newFilePath, newFileContent);
        }

        public async Task GenerateSolutionAsync(string path, string name)
        {
            _logger.LogInformation("GenerateSolution");
            var latestVersion = (await _packageFeed.GetVersionsAsync(PackageName)).First();
            _logger.LogInformation($"Latest version: {latestVersion}");
            if (Directory.GetFiles(path).Length > 0 || Directory.GetDirectories(path).Length > 0)
            {
                _logger.LogWarning($"Path {path} is not empty. Skipping solution generation");
                return;
            }
            await InitSolutionAsync(name, latestVersion, path);
        }

        private async Task InitSolutionAsync(string solutionName, string packageVersion, string path)
        {
            var packagesPath = Path.Combine(StudioDirectoryPath, "Packages");
            if (!Directory.Exists(packagesPath))
                Directory.CreateDirectory(packagesPath);
            var packageFilePath = await _packageFeed.DownloadPackageAsync(packagesPath, PackageName, packageVersion);
            _logger.LogInformation($"Downloaded version {packageVersion} to {packageFilePath}");

            CopyDirectory(packageFilePath, path);
            await RenameSolutionAsync(path, PackageName, solutionName);
            _logger.LogInformation($"Created solution in {path}");
        }

        private IEnumerable<Entity> SortByDependency(IEnumerable<Entity> entities)
        {
            var entityList = entities.ToList();
            var sortedList = new List<Entity>();
            while (entityList.Count > 0)
            {
                var okEntities = entityList
                    .Where(e => e.DependentEntities.Count == 0 ||
                                e.DependentEntities.All(de => sortedList.Contains(de)))
                    .ToList();
                if (okEntities.Count == 0)
                    throw new Exception("Cannot sort by dependency");
                foreach (var okEntity in okEntities)
                {
                    entityList.Remove(okEntity);
                    sortedList.Add(okEntity);
                }
            }

            return sortedList;
        }

        private async Task CopyEntitiesAsync(SolutionAccessor fromSolution, SolutionAccessor toSolution)
        {
            var fromEntities = await fromSolution.GetEntitiesAsync();
            fromEntities = SortByDependency(fromEntities).ToList();
            var toEntities = await toSolution.GetEntitiesAsync();
            foreach (var entity in fromEntities)
            {
                var toEntity = toEntities.FirstOrDefault(e => e.Id == entity.Id);
                if (toEntity == null)
                {
                    await toSolution.CreateEntityAsync(entity);
                    var newEntity = (await toSolution.GetEntitiesAsync())
                        .FirstOrDefault(e => e.Id == entity.Id);
                    await GenerateEntityAsync(toSolution, newEntity);
                    await GenerateViewAsync(toSolution, new View(), newEntity);
                }
                else
                {
                    await toSolution.UpdateEntityAsync(entity);
                    var newEntity = (await toSolution.GetEntitiesAsync())
                        .FirstOrDefault(e => e.Id == entity.Id);
                    await UpgradeEntityAsync(toSolution, toEntity, newEntity);
                    await UpgradeViewAsync(toSolution, new View(), toEntity, newEntity);
                }
            }
        }

        public async Task UpgradeSolutionAsync(SolutionAccessor solution)
        {
            _logger.LogInformation("UpgradeSolution");
            if (solution.Metadata == null)
            {
                _logger.LogWarning("Solution metadata not found. Cannot upgrade.");
                return;
            }
            _logger.LogInformation($"Current version: {solution.Metadata.Version}");
            var latestVersion = (await _packageFeed.GetVersionsAsync(PackageName)).First();
            _logger.LogInformation($"Latest version: {latestVersion}");
            if (solution.Metadata.Version == latestVersion)
            {
                _logger.LogWarning("Using latest version. Nothing to upgrade");
                return;
            }

            var solutionsPath = Path.Combine(StudioDirectoryPath, "Solutions");
            var latestPackageSolutionPath = Path.Combine(solutionsPath, $"{solution.Namespace}.{latestVersion}");
            if (Directory.Exists(latestPackageSolutionPath))
                Directory.Delete(latestPackageSolutionPath, true);
            var currentPackageSolutionPath = Path.Combine(solutionsPath, $"{solution.Namespace}.{solution.Metadata.Version}");
            if (Directory.Exists(currentPackageSolutionPath))
                Directory.Delete(currentPackageSolutionPath, true);

            await InitSolutionAsync(solution.Namespace, latestVersion, latestPackageSolutionPath);
            var latestPackageSolution = new SolutionAccessor(latestPackageSolutionPath);
            await CopyEntitiesAsync(solution, latestPackageSolution);

            await InitSolutionAsync(solution.Namespace, solution.Metadata.Version, currentPackageSolutionPath);
            var currentPackageSolution = new SolutionAccessor(currentPackageSolutionPath);
            await CopyEntitiesAsync(solution, currentPackageSolution);

            _logger.LogInformation($"Merging changes to {solution.Path}");
            await _fileMerge.MergeDirectoryAsync(currentPackageSolutionPath, latestPackageSolutionPath, solution.Path);
            _logger.LogInformation($"Completed");
        }

        private void CopyDirectory(string fromPath, string toPath)
        {
            foreach (string dirPath in Directory.GetDirectories(fromPath, "*",
                SearchOption.AllDirectories))
                Directory.CreateDirectory(dirPath.Replace(fromPath, toPath));

            foreach (string newPath in Directory.GetFiles(fromPath, "*.*",
                SearchOption.AllDirectories))
                File.Copy(newPath, newPath.Replace(fromPath, toPath), true);
        }

        private async Task RenameSolutionAsync(string path, string oldValue, string newValue)
        {
            foreach (var directory in Directory.GetDirectories(path))
            {
                var directoryName = Path.GetRelativePath(path, directory);
                var newDirectoryName = directoryName.Replace(oldValue, newValue);
                var newDirectory = Path.Combine(path, newDirectoryName);
                if (directory != newDirectory)
                {
                    if (Directory.Exists(newDirectory))
                        Directory.Delete(newDirectory);
                    Directory.Move(directory, newDirectory);
                }
                await RenameSolutionAsync(newDirectory, oldValue, newValue);
            }

            foreach (var file in Directory.GetFiles(path))
            {
                var fileName = Path.GetRelativePath(path, file);
                var newFileName = fileName.Replace(oldValue, newValue);
                var newFile = Path.Combine(path, newFileName);
                if (file != newFile)
                {
                    Directory.Move(file, newFile);
                }

                var fileContent = await ReadFileAsync(newFile);
                var newFileContent = fileContent.Replace(oldValue, newValue);
                if (fileContent != newFileContent)
                {
                    await WriteFileAsync(newFile, newFileContent);
                }
            }
        }

        private async Task<string> ReadFileAsync(string filePath)
        {
            using (var fileReader = new StreamReader(filePath))
            {
                return await fileReader.ReadToEndAsync();
            }
        }

        private async Task WriteFileAsync(string filePath, string content)
        {
            using (var fileWriter = new StreamWriter(filePath))
            {
                await fileWriter.WriteAsync(content);
            }
        }

        public async Task GenerateEntityAsync(SolutionAccessor solution, Entity entity)
        {
            var model = new EntityModel()
            {
                Entity = entity,
                Solution = solution,
            };

            var templates = await solution.LoadTemplatesAsync();
            foreach (var template in templates.Where(t => t.Type == TemplateType.Entity))
            {
                await CreateFileAsync(template, model, solution, entity);
            }
        }

        public async Task UpgradeEntityAsync(SolutionAccessor solution, Entity oldEntity, Entity newEntity)
        {
            var oldModel = new EntityModel()
            {
                Entity = oldEntity,
                Solution = solution,
            };

            var newModel = new EntityModel()
            {
                Entity = newEntity,
                Solution = solution,
            };

            var templates = await solution.LoadTemplatesAsync();
            foreach (var template in templates.Where(t => t.Type == TemplateType.Entity))
            {
                await UpgradeFileAsync(template, oldModel, newModel, solution, oldEntity, newEntity);
            }
        }

        public async Task GenerateViewAsync(SolutionAccessor solution, View view, Entity entity)
        {
            var model = new ViewModel()
            {
                View = view,
                Entity = entity,
                Solution = solution,
            };

            var templates = await solution.LoadTemplatesAsync();
            foreach (var template in templates.Where(t => t.Type == TemplateType.View))
            {
                await CreateFileAsync(template, model, solution, entity);
            }
        }

        public async Task UpgradeViewAsync(SolutionAccessor solution, View view, Entity oldEntity, Entity newEntity)
        {
            var oldModel = new ViewModel()
            {
                View = view,
                Entity = oldEntity,
                Solution = solution,
            };

            var newModel = new ViewModel()
            {
                View = view,
                Entity = newEntity,
                Solution = solution,
            };

            var templates = await solution.LoadTemplatesAsync();
            foreach (var template in templates.Where(t => t.Type == TemplateType.View))
            {
                await UpgradeFileAsync(template, oldModel, newModel, solution, oldEntity, newEntity);
            }
        }

        public async Task GenerateEnumAsync(SolutionAccessor solution, Models.Enum @enum)
        {
            var model = new EnumModel()
            {
                Enum = @enum,
                Solution = solution,
            };

            var templates = await solution.LoadTemplatesAsync();
            foreach (var template in templates.Where(t => t.Type == TemplateType.Enum))
            {
                await CreateFileAsync(template, model, solution, @enum);
            }
        }

        public async Task UpgradeEnumAsync(SolutionAccessor solution, Models.Enum oldEnum, Models.Enum newEnum)
        {
            var oldModel = new EnumModel()
            {
                Enum = oldEnum,
                Solution = solution,
            };

            var newModel = new EnumModel()
            {
                Enum = newEnum,
                Solution = solution,
            };

            var templates = await solution.LoadTemplatesAsync();
            foreach (var template in templates.Where(t => t.Type == TemplateType.Enum))
            {
                await UpgradeFileAsync(template, oldModel, newModel, solution, oldEnum, newEnum);
            }
        }

        public async Task GenerateEnumViewAsync(SolutionAccessor solution, View view, Models.Enum @enum)
        {
            var model = new EnumViewModel()
            {
                View = view,
                Enum = @enum,
                Solution = solution,
            };

            var templates = await solution.LoadTemplatesAsync();
            foreach (var template in templates.Where(t => t.Type == TemplateType.EnumView))
            {
                await CreateFileAsync(template, model, solution, @enum);
            }
        }

        public async Task UpgradeEnumViewAsync(SolutionAccessor solution, View view, Models.Enum oldEnum, Models.Enum newEnum)
        {
            var oldModel = new EnumViewModel()
            {
                View = view,
                Enum = oldEnum,
                Solution = solution,
            };

            var newModel = new EnumViewModel()
            {
                View = view,
                Enum = newEnum,
                Solution = solution,
            };

            var templates = await solution.LoadTemplatesAsync();
            foreach (var template in templates.Where(t => t.Type == TemplateType.EnumView))
            {
                await UpgradeFileAsync(template, oldModel, newModel, solution, oldEnum, newEnum);
            }
        }

        private bool HasWildcards(string path)
        {
            return path.Contains("*");
        }

        private string FindDirectory(string path, string templatePath)
        {
            var ignoreList = new List<string>() {"AebIt.Platform.Common"};
            var templatePathItems = templatePath.Split('\\');
            foreach (var templatePathItem in templatePathItems)
            {
                var dir = Directory.GetDirectories(path, templatePathItem)
                    .FirstOrDefault(d => ignoreList.All(l => !d.Contains(l)));
                if (dir == null)
                {
                    if (HasWildcards(templatePathItem))
                        throw new Exception($"Can't find directory {templatePath}");
                    dir = Path.Combine(path, templatePathItem);
                    Directory.CreateDirectory(dir);
                }

                path = dir;
            }

            return path;
        }
    }
}
