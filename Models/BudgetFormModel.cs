using System.ComponentModel.DataAnnotations;

namespace FinOps.Models;

public class BudgetFormModel
{
    [Required(ErrorMessage = "Budget name is required")]
    [StringLength(63, MinimumLength = 1, ErrorMessage = "Budget name must be between 1 and 63 characters")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Amount is required")]
    [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be at least $0.01")]
    public decimal? Amount { get; set; }

    [Required(ErrorMessage = "Time grain is required")]
    public string TimeGrain { get; set; } = "Monthly";

    [Required(ErrorMessage = "Start date is required")]
    public DateTime? StartDate { get; set; } = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);

    public DateTime? EndDate { get; set; }
}
