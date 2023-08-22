## DocumentaConAI

Programa escrito en C# de tipo consola que realiza la documentación de otros proyectos de C# y Javascript sin necesidad de que estos se encuentren en tiempo de ejecución.

En el programa se puede configurar para realizar documentación de las dependencias de cada proyecto
aquí hay compatibilidad con proyectos de Javascript como Node, Angular, React entre otros, así como
para proyectos de C# pues lee archivos package.json y .csproj


### Instalación
1. Clone el repositorio en la carpeta donde estén ubicados los proyectos que desea documentar.
2. Abra el proyecto en Visual Studio.
3. Edite el archivo program.cs dependiendo de la documentación que desea realizar (Ver tipos de documentaciones).
4. Ejecute el proyecto.

### Tipos de documentaciones
#### A. Documentación de Dependencias
En la sección del program.cs instancie la clase seccionDependencias indicando la ruta del proyecto y ejecute el método ToFile() para que se genere el archivo de documentación de dependencias.
```csharp
        var rootPath = @"C:\Proyectos";
        var seccionDependencias = new SeccionDependencias(rootPath);
        seccionDependencias.ToFile();
```

El resultado será un archivo en WORD por cada subdirectorio de la ruta indicada con la información de las depedencias.

#### B. Documentación de APIs
En la sección del program.cs instancie la clase ControllerDocumentationGenerator indicando la ruta del proyecto y ejecute el método GenerateApiDocumentationV2() para que se genere el archivo de documentación de API.

```csharp
        var rootPath = @"C:\Proyectos";
        var seccionApis = new ControllerDocumentationGenerator(rootPath);
        seccionApis.GenerateApiDocumentationV2();
```

El resultado será un archivo en WORD por cada subdirectorio de la ruta indicada con la información de los endpoints.

##### Características
- Identifica la ruta del endpoint y el método HTTP.
- Identifica el primer argumento de los métodos generando el Object Request.
- Identifica el tipo de retorno de los métodos generando el Object Response.
- Mapea tipos de datos primitivos y representa en tipo de dato en JSON con un ejemplo al estilo Swagger.
- Mapea tipos de datos complejos obteniendo información de sus propiedades por medio de Roslyn obteniendo información de sus propiedades y representando en tipo de dato en JSON con un ejemplo al estilo Swagger.


##### Restricciones

- Los tipos de objetos con tipos genéricos anidados de más de un nivel ejemplo Task<ActionResult<List<MiObjectResponse>>> se representan en JSON como un objeto vacío "Object": {}.
- Las propiedades de los objetos que tienen propiedades de tipo List<T> se representan en JSON como un objeto vacío "Object": {}.
- No se tiene en cuenta el prefijo del endpoint asignado por el framework para cada ruta del endpoint.