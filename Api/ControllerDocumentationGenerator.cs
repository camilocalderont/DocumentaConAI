using DocumentFormat.OpenXml;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text.RegularExpressions;


namespace DocumentaConAI.Api
{
    public class ControllerDocumentationGenerator
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

        };
        public ControllerDocumentationGenerator(string rootPath)
        {
            this.rootPath = rootPath;
        }


        public void GenerateApiDocumentationV2()
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
                    var syntaxTrees = csFiles.Select(file => CSharpSyntaxTree.ParseText(File.ReadAllText(file))).ToList();
                    List<MetadataReference> references = new List<MetadataReference>();
                    references.Add(MetadataReference.CreateFromFile(typeof(object).Assembly.Location));
                    compilation = CSharpCompilation.Create("MyCompilation")
                        .AddSyntaxTrees(syntaxTrees)
                        .AddReferences(references);

                    Dictionary<string, SemanticModel> semanticModels = new Dictionary<string, SemanticModel>();
                    foreach (var tree in syntaxTrees)
                    {
                        var root = (CompilationUnitSyntax)tree.GetRoot();
                        var namespaceDeclaration = root.DescendantNodes().OfType<NamespaceDeclarationSyntax>().FirstOrDefault();
                        var classesAndEnums = root.DescendantNodes().OfType<MemberDeclarationSyntax>()
                                                  .Where(d => d is ClassDeclarationSyntax || d is EnumDeclarationSyntax);

                        foreach (var member in classesAndEnums)
                        {
                            var semanticModel = compilation.GetSemanticModel(tree);
                            var namespaceName = member.AncestorsAndSelf().OfType<NamespaceDeclarationSyntax>().FirstOrDefault()?.Name.ToString();
                            var memberName = member is ClassDeclarationSyntax cls ? cls.Identifier.ToString() : ((EnumDeclarationSyntax)member).Identifier.ToString();
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

                        var controllers = root.DescendantNodes().OfType<ClassDeclarationSyntax>()
                            .Where(c => c.BaseList != null && c.BaseList.Types.Any(t => t.Type.ToString() == "ControllerBase"));

                        foreach (var controller in controllers)
                        {
                            var controllerInfo = ExtractControllerInfoV2(controller, semanticModels);
                            var projectDirectoryName = Path.GetFileName(projectDirectory);
                            if (!allControllers.ContainsKey(projectDirectoryName))
                            {
                                allControllers[projectDirectoryName] = new List<ControllerInfo>();
                            }
                            allControllers[projectDirectoryName].Add(controllerInfo);
                        }
                    }
                }
                string fileName = $"{rootPath}\\{projectName}.docx";
                ControllerInfoToWordConverter.ToWord(fileName, allControllers);
            }
        }

        private ControllerInfo ExtractControllerInfoV2(ClassDeclarationSyntax controller, Dictionary<string, SemanticModel> semanticModels)
        {
            var controllerInfo = new ControllerInfo
            {
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

                #region Obtener Ruta
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
                #endregion

                #region Obtener Object Request
                foreach (var param in endpoint.ParameterList.Parameters)
                {
                    if (ListTypeAnalyzer.IsListType(param.Type.ToString()))
                    {
                        string typeString = ListTypeAnalyzer.ExtractGenericType(param.Type.ToString());
                        Tuple<ITypeSymbol, SemanticModel> ObjectType = GetObjectType(typeString, semanticModels);

                        ITypeSymbol paramType = ObjectType.Item1;
                        SemanticModel semanticModel = ObjectType.Item2;
                        if (paramType != null)
                        {
                            //Agrego la expresión de Arreglo de objeto cuando es Lista
                            endpointInfo.RequestObject = "[{"+GetJsonRepresentation(paramType, semanticModel)+"}]";
                            endpointInfo.Parameters.Add(new MethodParameter { ParameterName = param.Identifier.ToString(), ParameterType = paramType.Name });
                        }

                    }
                    else
                    {
                        Tuple<ITypeSymbol, SemanticModel> ObjectType = GetObjectType(param.Type.ToString(), semanticModels);

                        ITypeSymbol paramType = ObjectType.Item1;
                        SemanticModel semanticModel = ObjectType.Item2;
                        if (paramType != null)
                        {
                            endpointInfo.RequestObject = GetJsonRepresentation(paramType, semanticModel);
                            endpointInfo.Parameters.Add(new MethodParameter { ParameterName = param.Identifier.ToString(), ParameterType = paramType.Name });
                        }
                    }


                }

                #endregion

                #region Obtener Object Response
                // Obtiene el tipo de objeto de la respuesta

                Console.WriteLine(endpoint.ReturnType);

                if (endpoint.ReturnType is GenericNameSyntax genericNameSyntax)
                {
                    string typeName = genericNameSyntax.TypeArgumentList.Arguments[0].ToString();
                    
                    if (ListTypeAnalyzer.IsListType(typeName))
                    {
                        string typeString = ListTypeAnalyzer.ExtractGenericType(typeName);
                        Tuple<ITypeSymbol, SemanticModel> ObjectTypeResponse = GetObjectType(typeString, semanticModels);
                        ITypeSymbol paramTypeResponse = ObjectTypeResponse.Item1;
                        SemanticModel semanticModelResponse = ObjectTypeResponse.Item2;
                        if (paramTypeResponse != null)
                        {
                            //Agrego la expresión de Arreglo de objeto cuando es Lista
                            endpointInfo.ResponseObject = "[{" + GetJsonRepresentation(paramTypeResponse, semanticModelResponse) + "}]";
                        }
                    }
                    else if (ActionResultTypeAnalyzer.IsActionResultType(typeName))
                    {
                        string typeString = ActionResultTypeAnalyzer.ExtractActionResultGenericType(typeName);
                        Tuple<ITypeSymbol, SemanticModel> ObjectTypeResponse = GetObjectType(typeString, semanticModels);
                        ITypeSymbol paramTypeResponse = ObjectTypeResponse.Item1;
                        SemanticModel semanticModelResponse = ObjectTypeResponse.Item2;
                        if (paramTypeResponse != null)
                        {
                            //Agrego la expresión de Arreglo de objeto cuando es ActionResult
                            endpointInfo.ResponseObject = "{" + GetJsonRepresentation(paramTypeResponse, semanticModelResponse) + "}";
                        }
                    }
                    else
                    {
                        Tuple<ITypeSymbol, SemanticModel> ObjectTypeResponse = GetObjectType(typeName, semanticModels);
                        ITypeSymbol paramTypeResponse = ObjectTypeResponse.Item1;
                        SemanticModel semanticModelResponse = ObjectTypeResponse.Item2;
                        if (paramTypeResponse != null)
                        {
                            endpointInfo.ResponseObject = GetJsonRepresentation(paramTypeResponse, semanticModelResponse);
                        }

                    }                   
                }
                else
                {
                    endpointInfo.ResponseObject = "{}";

                }
                #endregion

                controllerInfo.Endpoints.Add(endpointInfo);
            }

            return controllerInfo;
        }

        /// <summary>
        /// Obtiene el tipo de un objeto por medio de una lista de SemanticModel o de tipos primitivos por medio de un mapeador
        /// </summary>
        /// <param name="type"></param>
        /// <param name="endpointInfo"></param>
        /// <param name="semanticModels"></param>
        /// <returns>Tuple<ITypeSymbol, SemanticModel></returns>
        public Tuple<ITypeSymbol, SemanticModel> GetObjectType(string type, Dictionary<string, SemanticModel> semanticModels)
        {
            var matchingEntries = semanticModels
                .Where(kvp => kvp.Key.EndsWith("." + type))
                .Select(kvp => kvp.Value)
                .ToList();
            SemanticModel semanticModel = null;
            ITypeSymbol paramType = null;
            if (matchingEntries.Any())
            {
                semanticModel = matchingEntries[0];
                paramType = GetTypeByName(type, semanticModel.Compilation);
            }
            else
            {
                paramType = GetTypeSymbol(type, compilation);
            }

            return new Tuple<ITypeSymbol, SemanticModel>(paramType,semanticModel);
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



        private ITypeSymbol GetTypeSymbol(string typeName, Compilation compilation)
        {
            // Primero verifica si el tipo está en el mapa typeMap
            if (typeMap.TryGetValue(typeName, out var specialType))
            {
                return compilation.GetSpecialType(specialType);
            }

            // Si no está en el mapa, busca el tipo en la compilación
            var namedTypeSymbol = compilation.GetTypeByMetadataName(typeName);

            if (namedTypeSymbol != null)
            {
                return namedTypeSymbol;
            }

            // Si no se encuentra en ninguno de los dos, retorna un tipo genérico (por ejemplo, object)
            return compilation.GetSpecialType(SpecialType.System_Object);
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
                case SpecialType.System_Enum:
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

    }






}
