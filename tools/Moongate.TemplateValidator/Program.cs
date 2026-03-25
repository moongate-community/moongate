using ConsoleAppFramework;
using Moongate.TemplateValidator.Commands;

var app = ConsoleApp.Create();
app.Add<TemplateValidateCommand>("validate");

await app.RunAsync(args);
