using Budget_Manager.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace Budget_Manager.Controllers
{
    public class DashboardController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly CultureInfo cultureInfo = new CultureInfo("en-US");

        public DashboardController(ApplicationDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task<ActionResult> Index(int days = 7) // Default is 7 days
        {
            // Fetch all transactions
            List<Transaction> SelectedTransactions = await _context.Transactions
                .Include(x => x.Category)
                .ToListAsync();

            //Total Income
            int TotalIncome = SelectedTransactions
                .Where(i => i.Category?.Type == "Income")
                .Sum(j => j.Amount);
            ViewBag.TotalIncome = TotalIncome.ToString("C0", cultureInfo);

            //Total Expense
            int TotalExpense = SelectedTransactions
                .Where(i => i.Category?.Type == "Expense")
                .Sum(j => j.Amount);
            ViewBag.TotalExpense = TotalExpense.ToString("C0", cultureInfo);

            //Balance
            int Balance = TotalIncome - TotalExpense;
            ViewBag.Balance = Balance.ToString("C0", cultureInfo);

            //Doughnut Chart - Expense By Category
            ViewBag.DoughnutChartData = SelectedTransactions
                .Where(i => i.Category?.Type == "Expense")
                .GroupBy(j => j.Category?.CategoryId)
                .Select(k => new
                {
                    categoryTitleWithIcon = k.First().Category?.Icon + " " + k.First().Category?.Title,
                    amount = k.Sum(j => j.Amount),
                    formattedAmount = k.Sum(j => j.Amount).ToString("C0", cultureInfo),
                })
                .OrderByDescending(l => l.amount)
                .ToList();

            //Last 'days' Days
            DateTime StartDate = DateTime.Today.AddDays(-days + 1);
            DateTime EndDate = DateTime.Today;

            //Spline Chart - Income vs Expense

            //Income
            List<SplineChartData> IncomeSummary = SelectedTransactions
                .Where(i => i.Category?.Type == "Income" && i.Date >= StartDate && i.Date <= EndDate)
                .GroupBy(j => j.Date)
                .Select(k => new SplineChartData()
                {
                    day = k.First().Date.ToString("dd-MMM"),
                    income = k.Sum(l => l.Amount)
                })
                .ToList();

            //Expense
            List<SplineChartData> ExpenseSummary = SelectedTransactions
                .Where(i => i.Category?.Type == "Expense" && i.Date >= StartDate && i.Date <= EndDate)
                .GroupBy(j => j.Date)
                .Select(k => new SplineChartData()
                {
                    day = k.First().Date.ToString("dd-MMM"),
                    expense = k.Sum(l => l.Amount)
                })
                .ToList();

            //Combine Income & Expense
            string[] LastDays = Enumerable.Range(0, days)
                .Select(i => StartDate.AddDays(i).ToString("dd-MMM"))
                .ToArray();

            ViewBag.SplineChartData = from day in LastDays
                                      join income in IncomeSummary on day equals income.day into dayIncomeJoined
                                      from income in dayIncomeJoined.DefaultIfEmpty()
                                      join expense in ExpenseSummary on day equals expense.day into expenseJoined
                                      from expense in expenseJoined.DefaultIfEmpty()
                                      select new
                                      {
                                          day = day,
                                          income = income?.income ?? 0,
                                          expense = expense?.expense ?? 0,
                                      };

            //Recent Transactions
            ViewBag.RecentTransactions = await _context.Transactions
                .Include(i => i.Category)
                .OrderByDescending(j => j.Date)
                .Take(5)
                .ToListAsync();

            return View();
        }


        [HttpGet]
        public async Task<IActionResult> GetChartData(string days)
        {
            DateTime startDate;
            DateTime endDate = DateTime.Today;

            if (days == "all")
            {
                // If 'all' is selected, set the start date to the date of the earliest transaction.
                startDate = await _context.Transactions.MinAsync(t => t.Date);
            }
            else
            {
                // Otherwise, calculate the start date based on the number of days selected.
                startDate = DateTime.Today.AddDays(-int.Parse(days) + 1);
            }

            // Fetch all transactions
            List<Transaction> selectedTransactions = await _context.Transactions
                .Include(x => x.Category)
                .Where(i => i.Date >= startDate && i.Date <= endDate)
                .ToListAsync();

            //Income
            var incomeSummary = selectedTransactions
                .Where(i => i.Category?.Type == "Income")
                .GroupBy(j => j.Date)
                .ToDictionary(k => k.Key, k => k.Sum(l => l.Amount));

            //Expense
            var expenseSummary = selectedTransactions
                .Where(i => i.Category?.Type == "Expense")
                .GroupBy(j => j.Date)
                .ToDictionary(k => k.Key, k => k.Sum(l => l.Amount));

            //Combine Income & Expense
            var splineChartData = Enumerable.Range(0, (endDate - startDate).Days + 1)
                .Select(i => startDate.AddDays(i))
                .Select(date => new
                {
                    day = date.ToString("dd-MMM"),
                    income = incomeSummary.ContainsKey(date) ? incomeSummary[date] : 0,
                    expense = expenseSummary.ContainsKey(date) ? expenseSummary[date] : 0,
                });

            return Json(splineChartData);
        }

    }

    public class SplineChartData
    {
        public string day;
        public int income;
        public int expense;
    }
}
