using Moongate.Core.Directories;
using Moongate.Core.Extensions.Strings;
using Moongate.Core.Server.Types;
using Moongate.UO.Data.Professions;
using Moongate.UO.Data.Types;
using Moongate.UO.Interfaces.FileLoaders;
using Serilog;

namespace Moongate.UO.FileLoaders;

public class ProfessionsLoader : IFileLoader
{

    private readonly ILogger _logger = Log.ForContext<ProfessionsLoader>();
    private readonly DirectoriesConfig _directoriesConfig;

    public ProfessionsLoader(DirectoriesConfig directoriesConfig)
    {
        _directoriesConfig = directoriesConfig;
    }

    public async  Task LoadAsync()
    {

        var path = Path.Combine(_directoriesConfig[DirectoryType.Data], "prof.txt");
        if (!File.Exists(path))
        {
            var parent = Path.Combine(_directoriesConfig[DirectoryType.Data], "Professions");

            path = Path.Combine(parent, "SA", "Prof.txt");
        }

        if (File.Exists(path))
        {
            var maxProf = 0;
            List<ProfessionInfo> profs = [];

            using var s = File.OpenText(path);

            while (!s.EndOfStream)
            {
                var line = s.ReadLine();

                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                line = line.Trim();

                if (!line.InsensitiveStartsWith("Begin"))
                {
                    continue;
                }

                var prof = new ProfessionInfo();

                var totalStats = 0;
                var skillIndex = 0;
                var totalSkill = 0;

                while (!s.EndOfStream)
                {
                    line = await s.ReadLineAsync();

                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }

                    line = line.Trim();

                    if (line.InsensitiveStartsWith("End"))
                    {
                        if (prof.ID > 0 && totalStats >= 80 && totalSkill >= 100)
                        {
                            prof.FixSkills(); // Adjust skills array in case there are fewer skills than the default 4
                            profs.Add(prof);
                        }

                        break;
                    }

                    var cols = line.Split('\t', StringSplitOptions.RemoveEmptyEntries);
                    var key = cols[0].ToLowerInvariant();
                    var value = cols[1].Trim('"');

                    if (key == "type" && !value.InsensitiveEquals("profession"))
                    {
                        break;
                    }

                    switch (key)
                    {
                        case "truename":
                            {
                                prof.Name = value;
                            }
                            break;
                        case "nameid":
                            {
                                prof.NameID = int.Parse(value); //Utility.ToInt32(value);
                                break;
                            }
                        case "descid":
                            {
                                prof.DescID = int.Parse(value); //Utility.ToInt32(value);
                                break;
                            }
                        case "desc":
                            {
                                prof.ID = int.Parse(value); //Utility.ToInt32(value);
                                if (prof.ID > maxProf)
                                {
                                    maxProf = prof.ID;
                                }
                            }
                            break;
                        case "toplevel":
                            {
                                prof.TopLevel = value.ToLowerInvariant() == "true"; //Utility.ToBoolean(value);
                                break;
                            }
                        case "gump":
                            {
                                prof.GumpID = int.Parse(value); //Utility.ToInt32(value);
                                break;
                            }
                        case "skill":
                            {
                                if (!ProfessionInfo.TryGetSkillName(value, out var skillName))
                                {
                                    break;
                                }

                                var skillValue = byte.Parse(cols[2]);
                                prof.Skills[skillIndex++] = (skillName, skillValue);
                                totalSkill += skillValue;
                            }
                            break;
                        case "stat":
                            {
                                if (!Enum.TryParse(value, out StatType stat))
                                {
                                    break;
                                }

                                var statValue = byte.Parse(cols[2]);
                                prof.Stats[(int)stat >> 1] = statValue;
                                totalStats += statValue;
                            }
                            break;
                    }
                }
            }


            ProfessionInfo.Professions = new ProfessionInfo[maxProf + 1];

            foreach (var p in profs)
            {
                ProfessionInfo.Professions[p.ID] = p;
            }

            profs.Clear();
            profs.TrimExcess();
        }
        else
        {
            ProfessionInfo.Professions = new ProfessionInfo[1];
        }

        ProfessionInfo.Professions[0] = new ProfessionInfo
        {
            Name = "Advanced Skills"
        };

        _logger.Information("Loaded {Count} professions from {FilePath}", ProfessionInfo.Professions.Length, path);


    }
}
