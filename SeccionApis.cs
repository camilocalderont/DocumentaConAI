using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using DocumentFormat.OpenXml;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DocumentFormat.OpenXml.EMMA;
using System.Net;
using System.Text.RegularExpressions;
using System.ComponentModel;

namespace DocumentaConAI
{
    public class SeccionApis
    {
        private readonly string rootPath;
        private CSharpCompilation compilation;

        private static readonly List<string> HttpVerbs = new List<string>
        {
            "HttpGet",
            "HttpPost",
            "HttpPut",
            "HttpDelete",
            "HttpPatch"
        };

        private static readonly List<string> ListTypes = new List<string>
        {
            "IEnumerable",
            "List",
            "ICollection",
            "IEnumerator"
        };

        private static readonly string ListPattern = @"^(?<listType>" + string.Join("|", ListTypes) + @")\<(?<genericType>[^\>]+)\>$";

        private static readonly new Dictionary<string, SpecialType> typeMap = new Dictionary<string, SpecialType>
        {
            { "int", SpecialType.System_Int32 },
            { "bool", SpecialType.System_Boolean },
            { "byte", SpecialType.System_Byte },
            { "sbyte", SpecialType.System_SByte },
            { "short", SpecialType.System_Int16 },
            { "ushort", SpecialType.System_UInt16 },
            { "uint", SpecialType.System_UInt32 },
            { "long", SpecialType.System_Int64 },
            { "ulong", SpecialType.System_UInt64 },
            { "double", SpecialType.System_Double },
            { "float", SpecialType.System_Single },
            { "char", SpecialType.System_Char },
            { "string", SpecialType.System_String },
            { "IFormFile", SpecialType.System_Object },
            { "DateTime", SpecialType.System_DateTime },
            { "Decimal", SpecialType.System_Decimal },
            { "Enum", SpecialType.System_Enum },
            { "IEnumerable", SpecialType.System_Collections_IEnumerable },
            { "IEnumerable<", SpecialType.System_Collections_Generic_IEnumerable_T },
            { "List<", SpecialType.System_Collections_Generic_IList_T },
            { "ICollection<", SpecialType.System_Collections_Generic_ICollection_T },
            { "IEnumerator<", SpecialType.System_Collections_Generic_IEnumerator_T },

        };
        public SeccionApis(string rootPath)
        {
            this.rootPath = rootPath;
        }

        public void GenerateApiDocumentation()
        {
            var applications = Directory.GetDirectories(rootPath);
            foreach (var application in applications)
            {
                var projectName = Path.GetFileName(application);
                var projectDirectories = Directory.GetDirectories(application);

                Dictionary<string, List<ControllerInfo>> allControllers = new Dictionary<string, List<ControllerInfo>>();

                foreach (var projectDirectory in projectDirectories)
                {
                    var csFiles = Directory.GetFiles(projectDirectory, "*.cs", SearchOption.AllDirectories);
                    // Parsea cada archivo .cs en un SyntaxTree
                    var syntaxTrees = csFiles.Select(file => CSharpSyntaxTree.ParseText(File.ReadAllText(file))).ToList();

                    // Crea una lista para almacenar las referencias
                    List<MetadataReference> references = new List<MetadataReference>();

                    // Añade la referencia a mscorlib (y cualquier otra referencia necesaria)
                    references.Add(MetadataReference.CreateFromFile(typeof(object).Assembly.Location));
                    // Crea la compilación con todas las referencias y SyntaxTrees
                    this.compilation = CSharpCompilation.Create("MyCompilation")
                        .AddSyntaxTrees(syntaxTrees)
                        .AddReferences(references);

                    // Ahora puedes acceder a los modelos semánticos para cada árbol de sintaxis
                    Dictionary<string,SemanticModel> semanticModels = new Dictionary<string,SemanticModel>();
                    foreach (var tree in syntaxTrees)
                    {
                        var root = (CompilationUnitSyntax)tree.GetRoot();
                        var namespaceDeclaration = root.DescendantNodes().OfType<NamespaceDeclarationSyntax>().FirstOrDefault();

                        // Obtiene tanto clases como enumeradores
                        var classesAndEnums = root.DescendantNodes().OfType<MemberDeclarationSyntax>()
                                                  .Where(d => d is ClassDeclarationSyntax || d is EnumDeclarationSyntax);

                        foreach (var member in classesAndEnums)
                        {
                            var semanticModel = this.compilation.GetSemanticModel(tree);
                            var namespaceName = member.AncestorsAndSelf().OfType<NamespaceDeclarationSyntax>().FirstOrDefault()?.Name.ToString();

                            // Usamos una variable para representar el nombre, ya que ahora estamos tratando tanto con clases como con enumeradores
                            var memberName = (member is ClassDeclarationSyntax cls) ? cls.Identifier.ToString() : ((EnumDeclarationSyntax)member).Identifier.ToString();

                            if (namespaceName != null)
                            {
                                string fullyQualifiedName = namespaceName + "." + memberName;
                                if (!semanticModels.ContainsKey(fullyQualifiedName)) // Verifica si la clave ya existe en el diccionario
                                {
                                    semanticModels.Add(fullyQualifiedName, semanticModel);
                                }
                            }
                            else
                            {
                                semanticModels.Add(memberName, semanticModel);
                            }
                        }

                    }

                    foreach (var csFile in csFiles)
                    {
                        var syntaxTree = CSharpSyntaxTree.ParseText(File.ReadAllText(csFile));
                        var root = (CompilationUnitSyntax)syntaxTree.GetRoot();
                        var semanticModel = CSharpCompilation.Create("MyCompilation")
                            .AddSyntaxTrees(syntaxTree)
                            .AddReferences(MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
                            .GetSemanticModel(syntaxTree);

                        var controllers = root.DescendantNodes().OfType<ClassDeclarationSyntax>()
                            .Where(c => c.BaseList != null && c.BaseList.Types.Any(t => t.Type.ToString() == "ControllerBase"));

                        foreach (var controller in controllers)
                        {
                            var controllerInfo = ExtractControllerInfo(controller, semanticModels);
                            var projectDirectoryName = Path.GetFileName(projectDirectory);
                            if (!allControllers.ContainsKey(projectDirectoryName))
                            {
                                allControllers[projectDirectoryName] = new List<ControllerInfo>();
                            }
                            allControllers[projectDirectoryName].Add(controllerInfo);
                        }
                    }
                }

                WriteToWord(projectName, allControllers);
            }
        }

        private ControllerInfo ExtractControllerInfo(ClassDeclarationSyntax controller, Dictionary<string, SemanticModel> semanticModels)
        {
            var controllerInfo = new ControllerInfo
            {
                // Aquí se obtiene el nombre del controlador
                ControllerName = controller.Identifier.Text
            };

            // Obtén el atributo de la ruta del controlador, si está presente
            var controllerRouteAttribute = controller.AttributeLists.SelectMany(attrList => attrList.Attributes)
                .FirstOrDefault(attr => attr.Name.ToString() == "Route");
            var controllerRoute = controllerRouteAttribute != null && controllerRouteAttribute.ArgumentList.Arguments.Count > 0
                ? controllerRouteAttribute.ArgumentList.Arguments[0].ToString()
                : "";

            // Obtiene todos los métodos públicos en el controlador que tienen atributos HttpGet, HttpPost, etc.
            var endpoints = controller.Members
                .OfType<MethodDeclarationSyntax>()
                .Where(method => method.AttributeLists.Any(attrList => attrList.Attributes
                    .Any(attr => HttpVerbs.Contains(attr.Name.ToString()))))
                .ToList();

            foreach (var endpoint in endpoints)
            {
                var endpointInfo = new EndpointInfo();

                // Obtiene el verbo HTTP del endpoint
                endpointInfo.HttpMethod = endpoint.AttributeLists.SelectMany(attrList => attrList.Attributes)
                    .First(attr => HttpVerbs.Contains(attr.Name.ToString()))
                    .Name.ToString();

                var routeAttribute = endpoint.AttributeLists.SelectMany(attrList => attrList.Attributes)
                    .FirstOrDefault(attr => attr.Name.ToString() == "Route");


                if (routeAttribute != null)
                {
                    string ruta = routeAttribute.ArgumentList.Arguments[0].ToString().Trim('"');
                }


                if (routeAttribute != null && routeAttribute.ArgumentList.Arguments.Count > 0)
                {
                    endpointInfo.Route = routeAttribute.ArgumentList.Arguments[0].ToString();
                }
                else
                {
                    // Si no hay ruta en el método, usa la ruta del controlador
                    string rutaCompuestaEndpoint = "";
                    if (controllerRoute.Contains("[controller]"))
                    {
                        rutaCompuestaEndpoint += "/" + controllerInfo.ControllerName.Replace("Controller", "");
                    }

                    if (controllerRoute.Contains("[action]"))
                    {
                        rutaCompuestaEndpoint += "/" + endpoint.Identifier.Text;
                    }

                    var attributeWithArguments = endpoint.AttributeLists.SelectMany(attrList => attrList.Attributes)
                        .FirstOrDefault(attr => HttpVerbs.Contains(attr.Name.ToString()) && attr.ArgumentList?.Arguments.Count > 0);

                    string complementoRuta = "";
                    if (attributeWithArguments != null)
                    {
                        complementoRuta = attributeWithArguments.ArgumentList.Arguments[0].ToString().Trim('"');

                    }

                    endpointInfo.Route = complementoRuta.Length > 0 ? rutaCompuestaEndpoint + "/" + complementoRuta : rutaCompuestaEndpoint;
                }

                // Obtiene el tipo de objeto de la solicitud y de la respuesta
                foreach (var param in endpoint.ParameterList.Parameters)
                {
                    Console.WriteLine(param.Type.ToString());
                    if (IsListType(param.Type.ToString()))
                    {
                        //Si es lista crear objeto string así: [{attr1: type, attr2:type2}]
                    }
                    else
                    {
                        var matchingEntries = semanticModels
                                            .Where(kvp => kvp.Key.EndsWith("." + param.Type))
                                            .Select(kvp => kvp.Value)
                                            .ToList();
                        SemanticModel semanticModel = null;
                        ITypeSymbol paramType = null;
                        if (matchingEntries.Any())
                        {
                            // Usar matchingEntries[0] para obtener el primer SemanticModel que coincida
                            semanticModel = matchingEntries[0];

                            //paramType = semanticModel.GetTypeInfo(param.Type).Type;
                            paramType = GetTypeByName(param.Type.ToString(), semanticModel.Compilation);
                        }
                        else
                        {
                            var typeName = param.Type.ToString();
                            if (typeName == "IEnumerable<AtencionIndividualActor>")
                            {
                                Console.WriteLine("Test Error: " + controllerInfo.ControllerName + "," + endpointInfo.Route);
                            }
                            paramType = GetPrimitiveTypeSymbol(typeName, this.compilation);
                        }

                        if (paramType != null)
                        {

                            endpointInfo.RequestObject = GetJsonRepresentation(paramType, semanticModel);
                            endpointInfo.Parameters.Add(new MethodParameter { ParameterName = param.Identifier.ToString(), ParameterType = paramType.Name });
                        }
                    }
                    
                    
                }


                // Obtiene el tipo de objeto de la respuesta

                Console.WriteLine(endpoint.ReturnType);

                if (endpoint.ReturnType is GenericNameSyntax genericNameSyntax)
                {                    
                    var typeName = genericNameSyntax.TypeArgumentList.Arguments[0].ToString();
                    /*
                    if (IsListType(typeName))
                    {

                    }
                    else
                    {

                    }
                    */

                    var matchingEntries = semanticModels
                    .Where(kvp => kvp.Key.EndsWith("." + typeName))
                    .Select(kvp => kvp.Value)
                    .ToList();

                    SemanticModel semanticModel = null;
                    ITypeSymbol paramType = null;
                    if (matchingEntries.Any())
                    {
                        // Usa el primer SemanticModel que coincida
                        semanticModel = matchingEntries[0];
                        var returnType = GetTypeByName(typeName, semanticModel.Compilation);
                        if (returnType != null)
                        {
                            if (returnType is INamedTypeSymbol namedType && namedType.TypeArguments.Length > 0)
                            {
                                endpointInfo.ResponseObject = GetJsonRepresentation(namedType.TypeArguments[0], semanticModel);
                            }
                            else
                            {
                                endpointInfo.ResponseObject = GetJsonRepresentation(returnType, semanticModel);
                            }
                        }
                    }
                }
                else if (endpoint.ReturnType.ToString() == "ActionResult")
                {
                    // Cuando el tipo de retorno es ActionResult sin un tipo genérico, devolvemos un objeto vacío
                    endpointInfo.ResponseObject = "{}";
                }
                else
                {
                    endpointInfo.ResponseObject = "{}";

                }

                controllerInfo.Endpoints.Add(endpointInfo);
            }

            return controllerInfo;
        }

        private List<IPropertySymbol> GetAllProperties(ITypeSymbol type)
        {
            var properties = new List<IPropertySymbol>();
            while (type != null)
            {
                properties.AddRange(type.GetMembers().Where(m => m.Kind == SymbolKind.Property).Cast<IPropertySymbol>());
                type = (type as INamedTypeSymbol)?.BaseType;
            }

            // Si es un tipo genérico
            if (type is INamedTypeSymbol namedType && namedType.IsGenericType)
            {
                foreach (var arg in namedType.TypeArguments)
                {
                    // Si es una lista
                    if (arg is INamedTypeSymbol listType && listType.TypeArguments.Length > 0)
                    {
                        properties.AddRange(GetAllProperties(listType.TypeArguments[0]));
                    }
                    else
                    {
                        properties.AddRange(GetAllProperties(arg));
                    }
                }
            }

            return properties;
        }


        private ITypeSymbol GetPrimitiveTypeSymbol(string typeName, Compilation compilation)
        {

            
            // Comprobar si es un tipo genérico, por ejemplo, List<IFormFile>
            if (typeName.StartsWith("List<") && typeName.EndsWith(">"))
            {
                // Obtén el tipo de elemento de la lista (en este caso, "IFormFile")
                var elementType = typeName.Substring(5, typeName.Length - 6);

                if (typeMap.TryGetValue(elementType, out var specialType))
                {
                    return compilation.GetSpecialType(specialType);
                }
                else
                {
                    throw new ArgumentException($"Type name {elementType} does not match any known primitive types or handled generic types.");
                }
            }
            // si no es una lista, verifica si es un tipo primitivo
            else if (typeMap.TryGetValue(typeName, out var specialType))
            {
                return compilation.GetSpecialType(specialType);
            }
            else
            {
                throw new ArgumentException($"Type name {typeName} does not match any known primitive types or handled generic types.");
            }
        }


        public ITypeSymbol GetTypeByName(string typeName, Compilation compilation)
        {
            foreach (var syntaxTree in compilation.SyntaxTrees)
            {
                var semanticModel = compilation.GetSemanticModel(syntaxTree);
                var root = syntaxTree.GetRoot();
                var allClasses = root.DescendantNodes().OfType<ClassDeclarationSyntax>();

                foreach (var @class in allClasses)
                {
                    if (@class.Identifier.Text == typeName)
                    {
                        return semanticModel.GetDeclaredSymbol(@class);
                    }
                }
            }

            return null;
        }


        private string GetJsonRepresentation(ITypeSymbol type, SemanticModel semanticModel)
        {
            // Verificar si el tipo es primitivo
            if (IsPrimitiveType(type))
            {
                // Devuelve el valor por defecto para los tipos primitivos
                return $"\"{type.Name}\": {GetTypeDefaultValue(type)}";
            }
            else if (type.TypeKind == TypeKind.Class || type.TypeKind == TypeKind.Struct)
            {

                // Extrae las propiedades de la clase/struct
                var properties = GetAllProperties(type);
                // Convierte las propiedades en una representación JSON 
                return "{" + string.Join(", ", properties.Select(p => $"\"{p.Name}\": {GetTypeDefaultValue(p.Type)}")) + "}";
            }
            else if (type.TypeKind == TypeKind.Error)
            {
                // Si el TypeKind es Error, intenta obtener los nombres de las propiedades a partir de la sintaxis del tipo
                var classSyntax = type.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() as ClassDeclarationSyntax;
                if (classSyntax != null)
                {
                    var properties = classSyntax.Members.OfType<PropertyDeclarationSyntax>();
                    return "{" + string.Join(", ", properties.Select(p => $"\"{p.Identifier.Text}\": \"unknown\"")) + "}";
                }
            }

            // Devuelve el valor por defecto para otros casos
            return GetTypeDefaultValue(type);

        }

        private bool IsPrimitiveType(ITypeSymbol type)
        {
            switch (type.SpecialType)
            {
                case SpecialType.System_Boolean:
                case SpecialType.System_Byte:
                case SpecialType.System_SByte:
                case SpecialType.System_Int16:
                case SpecialType.System_UInt16:
                case SpecialType.System_Int32:
                case SpecialType.System_UInt32:
                case SpecialType.System_Int64:
                case SpecialType.System_UInt64:
                case SpecialType.System_Char:
                case SpecialType.System_Double:
                case SpecialType.System_Single:
                case SpecialType.System_String:
                case SpecialType.System_Object:
                    return true;
                default:
                    return false;
            }
        }

        private string GetTypeDefaultValue(ITypeSymbol type)
        {
            switch (type.SpecialType)
            {
                case SpecialType.System_Boolean:
                    return "true";
                case SpecialType.System_String:
                    return "\"string\"";
                case SpecialType.System_Int32:
                case SpecialType.System_Int64:
                case SpecialType.System_Decimal:
                case SpecialType.System_Double:
                    return "0";
                case SpecialType.System_DateTime:
                    return "\"2023-08-01T20:42:56.710Z\"";
                default:
                    // Para los tipos no primitivos, devuelve un objeto vacío
                    return "{}";
            }
        }


        public static bool IsListType(string typeDescription)
        {
            return Regex.IsMatch(typeDescription, ListPattern);
        }

        public static string ExtractGenericType(string typeDescription)
        {
            var match = Regex.Match(typeDescription, ListPattern);
            if (match.Success)
            {
                return match.Groups["genericType"].Value;
            }
            throw new ArgumentException("The provided type description is not a recognized list type.");
        }


        private void WriteToWord(string projectName, Dictionary<string, List<ControllerInfo>> allControllers)
        {
            if (allControllers.Values.Any(list => list.Count > 0))
            {
                string fileName = $"{this.rootPath}\\{projectName}.docx";
                using (WordprocessingDocument wordDocument = WordprocessingDocument.Create(fileName, WordprocessingDocumentType.Document))
                {
                    MainDocumentPart mainPart = wordDocument.AddMainDocumentPart();
                    mainPart.Document = new Document();
                    Body body = mainPart.Document.AppendChild(new Body());

                    // Create the table
                    Table table = new Table();
                    body.AppendChild(table);

                    // Add the header row
                    TableRow headerRow = new TableRow();
                    table.AppendChild(headerRow);

                    // Create header cells
                    TableCell headerCell1 = new TableCell(new Paragraph(new Run(new Text("Ruta del Endpoint"))));
                    TableCell headerCell2 = new TableCell(new Paragraph(new Run(new Text("Tipo de Método"))));
                    TableCell headerCell3 = new TableCell(new Paragraph(new Run(new Text("Request Object"))));
                    TableCell headerCell4 = new TableCell(new Paragraph(new Run(new Text("Response Object"))));
                    headerRow.Append(headerCell1);
                    headerRow.Append(headerCell2);
                    headerRow.Append(headerCell3);
                    headerRow.Append(headerCell4);

                    // Define cell borders
                    TableCellBorders cellBorders = new TableCellBorders(
                        new LeftBorder() { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 24, Color = "000000" },
                        new RightBorder() { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 24, Color = "000000" },
                        new TopBorder() { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 24, Color = "000000" },
                        new BottomBorder() { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 24, Color = "000000" }
                    );

                    // Add rows for the controllers
                    foreach (var appEntry in allControllers)
                    {
                        foreach (var controller in appEntry.Value)
                        {
                            foreach (var endpoint in controller.Endpoints)
                            {
                                // Create a new row
                                TableRow row = new TableRow();
                                table.AppendChild(row);

                                // Create cells for the row
                                TableCell cell1 = CreateTableCell(endpoint.Route, "18");
                                TableCell cell2 = CreateTableCell(endpoint.HttpMethod, "18");
                                TableCell cell3 = CreateTableCell(endpoint.RequestObject, "18");
                                TableCell cell4 = CreateTableCell(endpoint.ResponseObject, "18");

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
                    }

                    // Save changes to the document
                    mainPart.Document.Save();
                }
            }
        }

        // Método auxiliar para crear celdas de la tabla
        private TableCell CreateTableCell(string text, string fontSize)
        {
            var cell = new TableCell();
            var paragraph = new Paragraph();
            var run = new Run();
            var runProperties = new RunProperties(new FontSize { Val = fontSize });
            var textElement = new Text { Text = text };

            run.Append(runProperties);
            run.Append(textElement);
            paragraph.Append(run);
            cell.Append(paragraph);

            return cell;
        }

    }

    public class ControllerInfo
    {
        public string ControllerName { get; set; }
        public string FilePath { get; set; }
        public List<EndpointInfo> Endpoints { get; set; }

        public ControllerInfo()
        {
            Endpoints = new List<EndpointInfo>();
        }
    }

    public class EndpointInfo
    {
        public string HttpMethod { get; set; }
        public string Route { get; set; }
        public string RequestObject { get; set; }
        public string ResponseObject { get; set; }
        public List<MethodParameter> Parameters { get; set; }

        public EndpointInfo()
        {
            Parameters = new List<MethodParameter>();
        }
    }

    public class MethodParameter
    {
        public string ParameterName { get; set; }
        public string ParameterType { get; set; }
    }
}
