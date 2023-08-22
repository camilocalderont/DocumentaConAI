using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using DocumentFormat.OpenXml;
using Newtonsoft.Json.Linq;
using Sprache;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DocumentaConAI.Library
{
    public class LibraryDocumentationGenerator
    {
        private string rootPath;

        public LibraryDocumentationGenerator(string rootPath)
        {
            this.rootPath = rootPath;
        }

        public void ToFile()
        {
            var applications = Directory.GetDirectories(rootPath);
            foreach (var application in applications)
            {
                var projectName = Path.GetFileName(application);
                var projectDirectories = Directory.GetDirectories(application);
                //List<dynamic> allDependencies = new List<dynamic>();
                Dictionary<string, List<dynamic>> allDependencies = new Dictionary<string, List<dynamic>>();

                foreach (var projectDirectory in projectDirectories)
                {
                    var packagePath = Path.Combine(projectDirectory, "package.json");
                    var projectDirectoryName = Path.GetFileName(projectDirectory);

                    if (File.Exists(packagePath))
                    {
                        var packageJson = File.ReadAllText(packagePath);
                        var dependencies = JObject.Parse(packageJson)["dependencies"];
                        // Luego, en lugar de agregar directamente a allDependencies, agregar a la lista específica del repositorio.
                        if (!allDependencies.ContainsKey(projectDirectoryName))
                        {
                            allDependencies[projectDirectoryName] = new List<dynamic>();
                        }
                        allDependencies[projectDirectoryName].AddRange(dependencies);
                    }
                    else
                    {
                        var csprojFiles = Directory.GetFiles(projectDirectory, "*.csproj");
                        if (csprojFiles.Any())
                        {
                            searchIntoCsprojFile(csprojFiles, "", projectDirectoryName, allDependencies);
                        }
                        else
                        {
                            var projectSubDirectories = Directory.GetDirectories(projectDirectory);
                            foreach (var projectSubDirectory in projectSubDirectories)
                            {
                                var csprojSubFiles = Directory.GetFiles(projectSubDirectory, "*.csproj");
                                var projectSubDirectoryName = Path.GetFileName(projectSubDirectory);
                                if (csprojSubFiles.Any())
                                {
                                    searchIntoCsprojFile(csprojSubFiles, projectDirectoryName, projectSubDirectoryName, allDependencies);
                                }
                            }
                        }
                    }
                }

                WriteToWord(projectName, allDependencies);
            }
        }

        private static void searchIntoCsprojFile(string[] csprojFiles, string projectDirectoryName, string projectSubDirectoryName, Dictionary<string, List<dynamic>> allDependencies)
        {
            string repoName = projectDirectoryName.Length > 0 ? $"{projectDirectoryName}_{projectSubDirectoryName}" : projectSubDirectoryName;
            foreach (var csprojFile in csprojFiles)
            {
                var csprojText = File.ReadAllLines(csprojFile);
                var dependencies = csprojText.Where(line => line.Contains("<PackageReference"))
                    .Select(line => line.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                    .Where(parts => parts.Length >= 3)
                    .Select(parts => new
                    {
                        NameParts = parts[1].Split('='),
                        VersionParts = parts[2].Split('=')
                    })
                    .Where(parts => parts.NameParts.Length >= 2 && parts.VersionParts.Length >= 2)
                    .Select(parts => $"{parts.NameParts[1].Replace("\"", "").Replace(">", "")} : {parts.VersionParts[1].Replace("\"", "").Replace(">", "")}")
                    .ToList();

                if (!allDependencies.ContainsKey(repoName))
                {
                    allDependencies[repoName] = new List<dynamic>();
                }
                allDependencies[repoName].AddRange(dependencies);

            }
        }

        private void WriteToWord(string projectName, Dictionary<string, List<dynamic>> allDependencies)
        {
            if (allDependencies.Values.Any(list => list.Count > 0))
            {
                string fileName = $"{rootPath}\\{projectName}.docx";
                using (WordprocessingDocument wordDocument = WordprocessingDocument.Create(fileName, WordprocessingDocumentType.Document))
                {
                    MainDocumentPart mainPart = wordDocument.AddMainDocumentPart();
                    mainPart.Document = new Document();
                    Body body = mainPart.Document.AppendChild(new Body());

                    // Crear la tabla
                    Table table = new Table();
                    body.AppendChild(table);

                    // Agregar la fila de cabecera
                    TableRow headerRow = new TableRow();
                    table.AppendChild(headerRow);

                    // Crear las celdas de cabecera
                    TableCell headerCell1 = new TableCell(new Paragraph(new Run(new Text("Proyecto"))));
                    TableCell headerCell2 = new TableCell(new Paragraph(new Run(new Text("Repositorio"))));
                    TableCell headerCell3 = new TableCell(new Paragraph(new Run(new Text("Nombre Libreria"))));
                    TableCell headerCell4 = new TableCell(new Paragraph(new Run(new Text("Version"))));
                    headerRow.Append(headerCell1);
                    headerRow.Append(headerCell2);
                    headerRow.Append(headerCell3);
                    headerRow.Append(headerCell4);

                    // Define el borde de la celda
                    TableCellBorders cellBorders = new TableCellBorders(
                        new LeftBorder() { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 24, Color = "000000" },
                        new RightBorder() { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 24, Color = "000000" },
                        new TopBorder() { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 24, Color = "000000" },
                        new BottomBorder() { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 24, Color = "000000" }
                    );

                    RunProperties runProp = new RunProperties();
                    FontSize fontSize = new FontSize() { Val = "18" }; // Set font size (9pt = 18 half-points)
                    runProp.Append(fontSize);

                    // Agregar las filas de las dependencias
                    foreach (var repoEntry in allDependencies)
                    {
                        foreach (var dependency in repoEntry.Value)
                        {
                            // Crear una nueva fila
                            TableRow row = new TableRow();
                            table.AppendChild(row);

                            // Crear las celdas de la fila
                            TableCell cell1 = CreateTableCell(projectName, "18");
                            TableCell cell2 = CreateTableCell(repoEntry.Key, "18");
                            TableCell cell3;
                            TableCell cell4;


                            if (dependency is string depString)
                            {
                                string[] parts = depString.Split(new[] { ':' }, 2);
                                cell3 = CreateTableCell(parts[0].Trim(), "18");
                                cell4 = CreateTableCell(parts[1].Trim(), "18");
                            }
                            else if (dependency is JProperty jProp)
                            {
                                cell3 = CreateTableCell(jProp.Name, "18");
                                cell4 = CreateTableCell(jProp.Value.ToString(), "18");
                            }
                            else
                            {
                                continue; // Si no es ninguno de los tipos esperados, saltar esta iteración
                            }
                            cell1.Append(cellBorders.CloneNode(true));
                            cell2.Append(cellBorders.CloneNode(true));
                            cell3.Append(cellBorders.CloneNode(true));
                            cell4.Append(cellBorders.CloneNode(true));

                            row.Append(cell1);
                            row.Append(cell2);
                            row.Append(cell3);
                            row.Append(cell4);
                        }
                    }

                    // Guardar los cambios en el documento
                    mainPart.Document.Save();
                }
            }
        }

        private TableCell CreateTableCell(string content, string fontSize)
        {
            RunProperties runProp = new RunProperties();
            FontSize size = new FontSize() { Val = fontSize }; // Set font size (9pt = 18 half-points)
            runProp.Append(size);

            Run run = new Run(new Text(content));
            run.PrependChild((RunProperties?)runProp.CloneNode(true));

            return new TableCell(new Paragraph(run));
        }
    }
}
