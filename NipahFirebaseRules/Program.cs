using Jint;
using Jint.Runtime.Interop;
using Newtonsoft.Json;
using NipahFirebaseRules;
using NipahFirebaseRules.RuleScript;
using System.Dynamic;
using System.Text.RegularExpressions;
using Console = NConsole;

Regex importFind = new Regex("import[ ]*(?<filename>.+);");
Regex functionFind = new Regex("(@priority (?<priority>\\d+)(\\n|\\r|\\r\\n))*function[ ]+(?<fname>[a-z0-Z9]+)[ ]*\\(\\)[ ]*\\{[ ]*(\\n|\\r|\\r\\n)*(?<content>.*)(\\n|\\r|\\r\\n)*[ ]*\\}");
Regex requireFind = new Regex("require[ ]+(?<file>.*);");

Regex singleLineComments = new Regex("//(.*)");
Regex multiLineComments = new Regex("/\\*(?:(?!\\*/).)*\\*/", RegexOptions.Singleline);

while (true)
{
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine("\nNIPAH FIREBASE RULES");
    Console.ResetColor();

    Console.WriteLine("B: Build firebase rules to 'rule.json'");
    Console.WriteLine("H: Display help");
    Console.WriteLine("T: Test some command on rules (WIP)");
    Console.WriteLine("S: Setup initial environment for you development");
    Console.WriteLine("C: Create an arbitrary '.js' rule function file or '.rsc' rule file");
    Console.WriteLine("P: Preview the generated JSON for Firebase rules");

    var key = Console.ReadKey(true).Key;

    try
    {
        switch (key)
        {
            case ConsoleKey.B: build(); break;
            case ConsoleKey.H: help(); break;
            case ConsoleKey.T: test(); break;

            case ConsoleKey.S: setup(); break;
            case ConsoleKey.C: createFunction(); break;
            case ConsoleKey.P: preview(); break;
        }
    }
    catch(Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine(ex);
        Console.ResetColor();
    }

    Console.ReadKey(true);

    Console.Clear();
}

void transpileAll()
{
    string baseDir = Environment.CurrentDirectory;

    if (Directory.Exists("./Generated"))
    {
        string[] allJs = Directory.GetFiles(baseDir + "\\Generated\\", "*.js", SearchOption.AllDirectories);
        foreach (var js in allJs)
            File.Delete(js);
    }

    var compiler = new Compiler();

    var allFiles = Directory.GetFiles(baseDir, "*.rsc", SearchOption.AllDirectories);

    Directory.CreateDirectory("Generated");

    // string mainFile = null;

    HashSet<string> alreadyJoin = new HashSet<string>(32);
    string script = "";

    void transpile(string file)
    {
        if (alreadyJoin.Contains(file))
            return;

        var local = File.ReadAllText(file);

        local = singleLineComments.Replace(local, "");
        local = multiLineComments.Replace(local, "");

        // Detect imports
        var matches = importFind.Matches(local);
        foreach (Match match in matches)
        {
            string pointer = match.Groups["filename"].Value;
            transpile(pointer);
        }

        alreadyJoin.Add(file);

        script += '\n' + local;
    }

    foreach(var file in allFiles)
        transpile(file);

    var functions = functionFind.Matches(script);

    int size = functions.Count;

    (string name, string content, int priority)[] funs = new (string, string, int)[size];
    Dictionary<string, List<string>> jsImports = new (32);

    for (int i = 0; i < functions.Count; i++)
    {
        Match function = (Match)functions[i];

        string name = function.Groups["fname"].Value;
        string content = function.Groups["content"].Value;
        int priority = 0;
        var prior = function.Groups["priority"];
        if (prior.Success)
            priority = int.Parse(prior.Value);

        findRequires(name, ref content);

        funs[i] = (name, content, priority);
    }

    void findRequires(string name, ref string content)
    {
        var requires = requireFind.Matches(content);

        if (requires.Count == 0)
            return;

        var reqs = new List<string>(3);

        foreach(Match require in requires)
        {
            string file = require.Groups["file"].Value;

            if (!file.Contains(".js"))
                file += ".js";

            reqs.Add(file);
        }
        jsImports.Add(name, reqs);

        content = requireFind.Replace(content, "");
    }

    string processTranspiled(string name, string js)
    {
        if(jsImports.TryGetValue(name, out var imports))
        {
            foreach (var import in imports)
                js = js.Insert(0, $"Import(\"Generated/{import}\");\n");
        }
        return js;
    }

    var orderedFuns = from fun in funs orderby fun.priority select fun;

    foreach (var (name, content, _) in orderedFuns)
    {
        string finalFunction = compiler.TranspileToJs(content);
        File.WriteAllText("Generated/" + name + ".js", processTranspiled(name, finalFunction));
    }

    script = functionFind.Replace(script, "");

    findRequires("Main", ref script);

    string finalScript = null;
    bool terminated = false;
    var ftranspile = Task.Run(() =>
    {
        finalScript = compiler.TranspileToJs(script);
        terminated = true;
    });
    ftranspile.Wait(TimeSpan.FromSeconds(5));
    if(!terminated)
        throw new TimeoutException("Code encountered a loop problem, probably the syntax of your code is wrong");

    File.WriteAllText("Generated/Main.js", processTranspiled("Main", finalScript!));
}

MainRule getRules()
{
    transpileAll();

    MainRule root = (new RuleBuilder().Root() as MainRule)!;

    Engine engine = new Engine();

    HashSet<string> compiled = new HashSet<string>(32);

    void compile(string file)
    {
        if (!File.Exists(file))
            throw new FileNotFoundException("Cannot find specified file for importing", file);

        if (compiled.Contains(file))
            return;
        compiled.Add(Path.GetFullPath(file));

        engine.Execute(File.ReadAllText(file));

        var append = new RuleBuilder().Root() as MainRule;
        engine.Invoke("Build", append);
        root.AddFunction(Path.GetFileNameWithoutExtension(file), append!);
    }

    engine.SetValue("Comparison", TypeReference.CreateTypeReference<IfRule.IfType>(engine));

    engine.SetValue("Import", (string filename) => compile(filename));

    var files = Directory.GetFiles(Environment.CurrentDirectory, "*.js", SearchOption.AllDirectories);

    if(files.Length == 0)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("There are no files in this directory");
        Console.ResetColor();

        return null;
    }

    string mainFile = null, testFile = null;
    foreach(var file in files)
    {
        string name = Path.GetFileNameWithoutExtension(file).ToLower();
        if(name == "main")
        {
            mainFile = file;
            continue;
        }
        else if(name == "test")
        {
            testFile = file;
            continue;
        }

        compile(file);

        if (name == "main")
            engine.Invoke("Build", root);
    }
    
    engine.Execute(File.ReadAllText(mainFile!));
    engine.Invoke("Build", root);

    if(testFile != null)
    {
        engine.Execute(File.ReadAllText(testFile));
        bool result = engine.Invoke("Test", root).AsBoolean();
        Console.WriteLine($"For {Path.GetFileName(testFile)} the result is {result}");
    }

    return root;
}

void setup()
{
    NConsole.Options(("Firebase RuleScriptCode Setup", setupRsc),
        ("JavaScript Setup", setupJs));

    Console.WriteLine();

    Console.ForegroundColor = ConsoleColor.Blue;
    Console.WriteLine("The Initial Setup was done!");
    Console.ResetColor();
}

void setupRsc()
{
    using var rscMainStream = new StreamReader(typeof(Program).Assembly.GetManifestResourceStream("NipahFirebaseRules.Main.rsc"));
    string rscMain = rscMainStream.ReadToEnd();
    File.WriteAllText("Main.rsc", rscMain);
    Console.WriteLine("Created 'Main.rsc'");
}

void setupJs()
{
    using var jsRefStream = new StreamReader(typeof(Program).Assembly.GetManifestResourceStream("NipahFirebaseRules.JavaScriptReference-NFR.ts"));
    string jsRef = jsRefStream.ReadToEnd();
    File.WriteAllText("JavaScriptReference-NFR.ts", jsRef);
    Console.WriteLine("Created 'JavaScriptReference-NFR.ts'");
    Console.WriteLine("...");
    File.WriteAllText("Main.js", @"function Build(/** @type Rule */ root) {
    // Write your main rule code here: E.g. root.Match(...)...
}");
    Console.WriteLine("Created 'Main.js'");
}

void createFunctionJs()
{
    Console.WriteLine("Type the name of your function:");
    Console.Write("> ");
    string name = Console.ReadLine();

    File.WriteAllText(name + ".js", @$"function Build(/** @type Rule */ root) {{
    // Write your {name} function code here: E.g. root.If(...)...
}}");
    Console.WriteLine($"Created '{name}.js'!");
}
void createFunctionRsc()
{
    Console.WriteLine("Type the name of your RuleScriptCode file:");
    Console.Write("> ");
    string name = Console.ReadLine();

    File.WriteAllText(name + ".rsc", @$"function Default() {{
    if true == true
}}");
    Console.WriteLine($"Created '{name}.rsc'!");
}

void createFunction()
{
    Console.Options(("JavaScript Function File", createFunctionJs),
        ("Firebase RuleScriptCode File", createFunctionRsc));

    Console.WriteLine();

    Console.ForegroundColor = ConsoleColor.Blue;
    Console.WriteLine("The Initial Setup was done!");
    Console.ResetColor();
}

void preview()
{
    string json = buildJSON();

    Console.ForegroundColor = ConsoleColor.Blue;
    Console.WriteLine("Preview of Firebase rules");

    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine(json);

    Console.ResetColor();
}

string buildJSON()
{
    var rules = getRules();

    if (rules == null)
        return null;

    dynamic mainRoot = new ExpandoObject();

    mainRoot.rules = rules.Build();

    string json = JsonConvert.SerializeObject(mainRoot, Formatting.Indented);

    return json;
}

void build()
{
    string json = buildJSON();

    if (json == null)
        return;

    Directory.CreateDirectory("./Build");

    File.WriteAllText("Build/Rules.json", json);

    Console.WriteLine("Rules build sucessfully to 'Build/Rules.json'!");
}
void help()
{
    Console.WriteLine(
        @"--- Showing help of this tool ---
Make a file called Main.js and put all your main rule code inside
You can put other .js files in directory but they will be handled as functions of your main file
Be aware, without a Main file the builder will throw an error

Also, for rule testing, make a file called Test.js on your directory, the test file will run after all other .js files including Main

To test arbitrary commands, use [T] and type in form: write|read path/is/separated/this/way: a valid jsonValue

To setup rule development (minimal) use [S], this will create a file called 'JavaScriptReference-NFR.ts' wich contains type definitions for use on your favorite development environment. This will also create a file named 'Main.js', the main file of you rule system

To create arbitrary rule definition files use [C] then type the name of the function you want to create, the system will generate the file for you use

To preview generated JSON rules of Firebase, use [P]

Happy rule building! :)

");
}
void test()
{
    Console.WriteLine("Type your test:");
    Console.Write("> ");
    string test = Console.ReadLine();

    bool commandSet = false, pathSet = false;

    string command = null;
    string path = null;
    string value = null;
    foreach(var c in test)
    {
        if (!commandSet)
        {
            if (c == ' ')
            {
                commandSet = true;
                continue;
            }
            command += c;
        }
        else if (!pathSet)
        {
            if (c == ':')
            {
                pathSet = true;
                continue;
            }
            path += c;
        }
        else
            value += c;
    }

    TestOperation op = command switch
    {
        "write" => TestOperation.Write,
        "read" => TestOperation.Read,
        _ => TestOperation.Write
    };

    dynamic testValue = JsonConvert.DeserializeObject(value);

    var rules = getRules();

    if(rules == null)
    {
        Console.WriteLine("The test returned: No valid rules");
        return;
    }

    bool result = rules.Test(op, testValue, path.Split('/'));

    Console.WriteLine($"The test returned: {result}");
}