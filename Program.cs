using System;
using System.IO;
using System.Linq;
using DocumentaConAI;
using Newtonsoft.Json.Linq;
using OpenAI;
using DotNetEnv;
using OpenAI.Models;

class Program
{
    static async Task Main()
    {
        string envPath = @"C:\Proyectos\SOAINT\DocumentaConAI\.env";
        DotNetEnv.Env.Load(envPath);

        var openAIKey = Environment.GetEnvironmentVariable("OPENAI_KEY"); //imprime null
        var fileName = @"C:\\Proyectos\\SOAINT\\output.txt";                                                                  //Console.WriteLine(openAIKey);
                                                                                                                              //var fileName = @"C:\\Proyectos\\SOAINT\\output.txt";

        var rootPath = @"C:\Proyectos\SOAINT";
        #region dependencias
        //var seccionDependencias = new SeccionDependencias(rootPath);
        //seccionDependencias.ToFile();
        #endregion

        #region apis
        var seccionApis = new SeccionApis(rootPath);
        seccionApis.GenerateApiDocumentation();
        #endregion
        /*
        
        var longTextArray = File.ReadAllLines(fileName);
        var longText = string.Join("\n", longTextArray);
        var splitter = new TextSplitter();
        var chunks = splitter.SplitText(longText);
        string response = "";
        string head = "Actúa como un desarrollador full stack especialista en .Net y Angular  que trabaja para una empresa llamada SOAINT, Tu tarea es realizar la siguiente tarea:  una tabla que contenga las siguientes columnas [proyecto = nombre archivo,nombre libreria, version]";
        foreach (var chunk in chunks)
        {
            string prompt = head + "\n" + chunk;
            response += await callOpenAi(prompt);
        }
        Console.WriteLine(response);
        */
    }

    public static async Task<string> callOpenAi(string prompt)
    {
        try
        {
            var api = new OpenAIClient("");
            var result = await api.CompletionsEndpoint.CreateCompletionAsync(prompt, temperature: 0.2, topP: 0.3, frequencyPenalty: 0.5, maxTokens: 2000, model: Model.Davinci) ;
            return result;

        }
        catch (Exception ex)
        {
            return ex.ToString();
        }
    }
}
