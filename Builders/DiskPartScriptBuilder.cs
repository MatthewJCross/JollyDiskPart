using System.Text;

namespace JollyDiskPart.Builders
{
    public class DiskPartScriptBuilder
    {
        private readonly List<string> _commands = new();

        public DiskPartScriptBuilder Add(string command)
        {
            if (!string.IsNullOrWhiteSpace(command))
            {
                _commands.Add(command.Trim());
            }

            return this;
        }

        public DiskPartScriptBuilder SelectDisk(int diskNumber)
        {
            _commands.Add($"select disk {diskNumber}");
            return this;
        }

        public DiskPartScriptBuilder SelectPartition(int partitionNumber)
        {
            _commands.Add($"select partition {partitionNumber}");
            return this;
        }

        public DiskPartScriptBuilder DetailPartition()
        {
            _commands.Add("detail partition");
            return this;
        }

        public DiskPartScriptBuilder SelectVolume(int volumeNumber)
        {
            _commands.Add($"select volume {volumeNumber}");
            return this;
        }

        public DiskPartScriptBuilder DetailVolume()
        {
            _commands.Add("detail volume");
            return this;
        }

        public DiskPartScriptBuilder Clean()
        {
            _commands.Add("clean");
            return this;
        }

        public DiskPartScriptBuilder CleanAll()
        {
            _commands.Add("clean all");
            return this;
        }

        public DiskPartScriptBuilder CreatePrimaryPartition(long sizeMb = 0, long offsetKb = 0)
        {
            var command = "create partition primary";

            if (sizeMb > 0)
                command += $" size={sizeMb}";

            if (offsetKb > 0)
                command += $" offset={offsetKb}";

            _commands.Add(command);
            return this;
        }

        public DiskPartScriptBuilder DeletePartition()
        {
            _commands.Add("delete partition override");
            return this;
        }

        public DiskPartScriptBuilder ListPartitions()
        {
            _commands.Add("list partition");
            return this;
        }

        public DiskPartScriptBuilder Format(string filesystem = "ntfs", string ?label = null, bool quick = true)
        {
            StringBuilder command = new();
            command.Append($"format fs={filesystem.ToUpperInvariant()}");

            if (quick)
            {
                command.Append(" quick");
            }

            if (!string.IsNullOrWhiteSpace(label))
            {
                command.Append($" label=\"{label}\"");
            }

            _commands.Add(command.ToString());

            return this;
        }

        public DiskPartScriptBuilder ExtendPartition(long sizeMB)
        {
            if (sizeMB > 0)
            {
                _commands.Add($"extend size={sizeMB}");
            }
            else
            {
                _commands.Add("extend");
            }

            return this;
        }

        public DiskPartScriptBuilder ShrinkPartition(long sizeMB)
        {
            _commands.Add($"shrink desired={sizeMB}");
            return this;
        }

        public DiskPartScriptBuilder AssignLetter(char letter)
        {
            _commands.Add($"assign letter={letter}");
            return this;
        }

        public DiskPartScriptBuilder RemoveLetter(char letter)
        {
            _commands.Add($"remove letter={letter}");
            return this;
        }

        public DiskPartScriptBuilder Extend()
        {
            _commands.Add("extend");
            return this;
        }

        public DiskPartScriptBuilder Shrink(long sizeMb)
        {
            _commands.Add($"shrink desired={sizeMb}");
            return this;
        }

        public DiskPartScriptBuilder ConvertGPT()
        {
            _commands.Add("convert gpt");
            return this;
        }

        public DiskPartScriptBuilder ConvertMBR()
        {
            _commands.Add("convert mbr");
            return this;
        }

        public DiskPartScriptBuilder Rescan()
        {
            _commands.Add("rescan");
            return this;
        }

        public DiskPartScriptBuilder Exit()
        {
            _commands.Add("exit");
            return this;
        }

        public DiskPartScriptBuilder QueryMaximumShrink()
        {
            _commands.Add("shrink querymax");
            return this;
        }

        public DiskPartScriptBuilder RemoveDriveLetter()
        {
            _commands.Add("remove");
            return this;
        }

        public DiskPartScriptBuilder AssignDriveLetter(char driveLetter)
        {
            _commands.Add($"assign letter={char.ToUpper(driveLetter)}");
            return this;
        }

        public DiskPartScriptBuilder ConvertGpt()
        {
            _commands.Add("convert gpt");
            return this;
        }

        public DiskPartScriptBuilder ConvertMbr()
        {
            _commands.Add("convert mbr");
            return this;
        }

        public DiskPartScriptBuilder OfflineDisk()
        {
            _commands.Add("offline disk");
            return this;
        }

        public DiskPartScriptBuilder OnlineDisk()
        {
            _commands.Add("online disk");
            return this;
        }

        public DiskPartScriptBuilder CleanDisk(bool cleanAll = false)
        {
            _commands.Add(cleanAll ? "clean all" : "clean");
            return this;
        }

        public DiskPartScriptBuilder ConvertDisk(bool gpt)
        {
            _commands.Add(gpt ? "convert gpt" : "convert mbr");
            return this;
        }

        public DiskPartScriptBuilder ClearReadOnlyDisk()
        {
            _commands.Add("attributes disk clear readonly");
            return this;
        }

        public DiskPartScriptBuilder SelectLastPartition()
        {
            _commands.Add("select partition last");
            return this;
        }

        public string Build()
        {
            StringBuilder script = new();

            foreach (var command in _commands)
            {
                script.AppendLine(command);
            }

            return script.ToString();
        }

        public override string ToString()
        {
            return Build();
        }

        public void Clear()
        {
            _commands.Clear();
        }
    }
}
