namespace JollyDiskPart.Models
{
    public class DiskOperationResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string Output { get; set; } = string.Empty;
        public string Error { get; set; } = string.Empty;
        public string Script { get; set; } = string.Empty;
        public DateTime Started { get; set; }
        public DateTime Completed { get; set; }
        public TimeSpan Duration => Completed - Started;
        public int ExitCode { get; set; }

        public static DiskOperationResult Ok(string message = "")
        {
            return new DiskOperationResult
            {
                Success = true,
                Message = message,
                Started = DateTime.Now,
                Completed = DateTime.Now
            };
        }

        public static DiskOperationResult Fail(string error)
        {
            return new DiskOperationResult
            {
                Success = false,
                Error = error,
                Message = "Operation failed",
                Started = DateTime.Now,
                Completed = DateTime.Now
            };
        }

        public override string ToString()
        {
            return Success ? Output : $"FAILED\n{Error}";
        }
    }
}

