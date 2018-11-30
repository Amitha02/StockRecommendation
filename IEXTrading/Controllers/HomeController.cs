﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using IEXTrading.Infrastructure.IEXTradingHandler;
using IEXTrading.Models;
using IEXTrading.Models.ViewModel;
using IEXTrading.DataAccess;
using Newtonsoft.Json;
using Microsoft.AspNetCore.Http;

namespace MVCTemplate.Controllers
{
    public class HomeController : Controller
    {
        public ApplicationDbContext dbContext;
        internal static List<CompaniesEquities> companiesEquities = new List<CompaniesEquities>();
        internal static List<Gainers> gainers = new List<Gainers>();
        internal static List<Losers> losers = new List<Losers>();
        public HomeController(ApplicationDbContext context)
        {
            dbContext = context;
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult AboutUs()
        {
            return View();
        }

        /****
         * The Symbols action calls the GetSymbols method that returns a list of Companies.
         * This list of Companies is passed to the Symbols View.
        ****/
        public IActionResult Symbols()
        {
            //Set ViewBag variable first
            ViewBag.dbSucessComp = 0;
            IEXHandler webHandler = new IEXHandler();
            List<Company> companies = webHandler.GetSymbols();

            //Save comapnies in TempData
            String companiesData = JsonConvert.SerializeObject(companies);
            HttpContext.Session.SetString("Companies", companiesData);

            return View(companies);
        }

        /****
         * The Chart action calls the GetChart method that returns 1 year's equities for the passed symbol.
         * A ViewModel CompaniesEquities containing the list of companies, prices, volumes, avg price and volume.
         * This ViewModel is passed to the Chart view.
        ****/
        public IActionResult Chart(string symbol)
        {
            //Set ViewBag variable first
            ViewBag.dbSuccessChart = 0;
            List<Equity> equities = new List<Equity>();
            if (symbol != null)
            {
                ViewBag.SelectedItem = symbol;
                IEXHandler webHandler = new IEXHandler();
                equities = webHandler.GetChart(symbol);
                equities = equities.OrderBy(c => c.date).ToList(); //Make sure the data is in ascending order of date.
            }

            CompaniesEquities companiesEquities = getCompaniesEquitiesModel(equities);

            return View(companiesEquities);
        }

        /****
         * The Chart action calls the GetaMonthEquitiesforSymbol method that returns 1 month's equities for the passed symbol.
         * A getRecommendationCompaniesEquitiesModel method returns ViewModel CompaniesEquities containing the list of companies, prices, volumes, avg price, 
           volume, maximum high price, minimum high price and Recommendation to Buy/Sell.
         * This ViewModel is passed to the Recommendation view.
        ****/
        public IActionResult Recommendation()
        {
            //Set ViewBag variable first
            ViewBag.dbSuccessChart = 0;
            ChartCompaniesEquities chart = new ChartCompaniesEquities();
            CompaniesEquities recommendationEquity;
            List<CompaniesEquities> tempCompaniesEquities = new List<CompaniesEquities>();
            
            List<Equity> equities = new List<Equity>();
            IEXHandler webHandler = new IEXHandler();
            List<Company> companies = dbContext.Companies.ToList();
            //companies = companies.GetRange(0, 5);
            companies = companies.Where(e => e.type.Equals("su")).ToList();

            if (companies.Count > 0)
            {
                foreach (Company company in companies)
                {
                    webHandler = new IEXHandler();
                    if (companiesEquities == null)
                        companiesEquities = new List<CompaniesEquities>();
                    if (gainers == null)
                        gainers = new List<Gainers>();
                    if (losers == null)
                        losers = new List<Losers>();

                    if (companiesEquities.Where(c => c != null && c.Current.symbol.Equals(company.symbol)).Count() == 0)
                    {
                        equities = webHandler.GetaMonthEquitiesforSymbol(company.symbol);
                        if (equities.Count > 0)
                        {
                            equities = equities.OrderBy(c => c.date).ToList(); //Make sure the data is in ascending order of date. 

                            recommendationEquity = getRecommendationCompaniesEquitiesModel(equities);
                            companiesEquities.Add(recommendationEquity);
                        }
                    }
                }

                tempCompaniesEquities = companiesEquities.OrderByDescending(o => o.Current.changePercent).ToList();
                tempCompaniesEquities = tempCompaniesEquities.GetRange(0, 3);
                gainers = new List<Gainers>();
                foreach (CompaniesEquities equity in tempCompaniesEquities)
                {
                    Gainers gain = new Gainers();
                    gain.Symbol = equity.Current.symbol;
                    gain.CurrentPrice = equity.Current.close;
                    gain.ChangePercent = equity.Current.changePercent;
                    gainers.Add(gain);
                }

                tempCompaniesEquities = null;
                tempCompaniesEquities = companiesEquities.OrderBy(o => o.Current.changePercent).ToList();
                tempCompaniesEquities = tempCompaniesEquities.GetRange(0, 3);
                losers = new List<Losers>();
                foreach (CompaniesEquities equity in tempCompaniesEquities)
                {
                    Losers lose = new Losers();
                    lose.Symbol = equity.Current.symbol;
                    lose.CurrentPrice = equity.Current.close;
                    lose.ChangePercent = equity.Current.changePercent;
                    losers.Add(lose);
                }

                companiesEquities = companiesEquities.OrderByDescending(o => o.AvgPrice).ToList();

                if (companiesEquities.Count > 5)
                    companiesEquities = companiesEquities.GetRange(0, 5);

                chart.chartCompaniesEquities = companiesEquities;
                chart.chartGainers = gainers;
                chart.chartLosers = losers;

            }
            return View(chart);
        }

        public IActionResult Gainers()
        {
            return PartialView();
        }
            /****
             * The Refresh action calls the ClearTables method to delete records from a or all tables.
             * Count of current records for each table is passed to the Refresh View.
            ****/
            public IActionResult Refresh(string tableToDel)
        {
            ClearTables(tableToDel);
            Dictionary<string, int> tableCount = new Dictionary<string, int>();
            tableCount.Add("Companies", dbContext.Companies.Count());
            tableCount.Add("Charts", dbContext.Equities.Count());
            tableCount.Add("Recommendation", dbContext.RecommendationEquities.Count());
            companiesEquities = null;
            return View(tableCount);
        }

        /****
         * Saves the Symbols in database.
        ****/
        public IActionResult PopulateSymbols()
        {
            string companiesData = HttpContext.Session.GetString("Companies");
            //List<Company> companies = JsonConvert.DeserializeObject<List<Company>>(TempData["Companies"].ToString());
            List<Company> companies = null;
            if (companiesData != "")
            {
                companies = JsonConvert.DeserializeObject<List<Company>>(companiesData);
            }
            foreach (Company company in companies)
            {
                //Database will give PK constraint violation error when trying to insert record with existing PK.
                //So add company only if it doesnt exist, check existence using symbol (PK)
                if (dbContext.Companies.Where(c => c.symbol.Equals(company.symbol)).Count() == 0)
                {
                    dbContext.Companies.Add(company);
                }
            }
            dbContext.SaveChanges();
            ViewBag.dbSuccessComp = 1;
            return View("Symbols", companies);
        }

        /****
         * Saves the equities in database.
        ****/
        public IActionResult SaveCharts(string symbol)
        {
            IEXHandler webHandler = new IEXHandler();
            List<Equity> equities = webHandler.GetChart(symbol);
            //List<Equity> equities = JsonConvert.DeserializeObject<List<Equity>>(TempData["Equities"].ToString());
            foreach (Equity equity in equities)
            {
                if (dbContext.Equities.Where(c => c.date.Equals(equity.date)).Count() == 0)
                {
                    dbContext.Equities.Add(equity);
                }
            }

            dbContext.SaveChanges();
            ViewBag.dbSuccessChart = 1;

            CompaniesEquities companiesEquities = getCompaniesEquitiesModel(equities);

            return View("Chart", companiesEquities);
        }

        /****
        * Saves the Recommendation Equities to the database.
       ****/
        public IActionResult SaveRecommendation()
        {
            List<CompaniesEquities> equities = companiesEquities;
            RecommendationEquity recommendedEquity;
            dbContext.RecommendationEquities.RemoveRange(dbContext.RecommendationEquities);
            dbContext.SaveChanges();
            foreach (CompaniesEquities equity in equities)
            {
                recommendedEquity = new RecommendationEquity();
                if (dbContext.RecommendationEquities.Where(c => c.Symbol.Equals(equity.Current.symbol)).Count() == 0)
                {
                    recommendedEquity.Symbol = equity.Current.symbol;
                    recommendedEquity.LastDate = equity.Current.date;
                    recommendedEquity.LastOpen = equity.Current.open;
                    recommendedEquity.LastHigh = equity.Current.high;
                    recommendedEquity.LastLow = equity.Current.low;
                    recommendedEquity.LastClose = equity.Current.close;
                    recommendedEquity.LastVolume = equity.Current.volume;
                    recommendedEquity.AverageVolume = equity.AvgVolume;
                    recommendedEquity.AveragePrice = equity.AvgPrice;
                    recommendedEquity.HighPrice = equity.HighPrice;
                    recommendedEquity.LowPrice = equity.LowPrice;
                    recommendedEquity.Recommendation = equity.Recommendation;

                    dbContext.RecommendationEquities.Add(recommendedEquity);
                }
            }

            dbContext.SaveChanges();
            ViewBag.dbSuccessRecommendation = 1;

            //RecommendationEquity companiesEquities = getRecommendationCompaniesEquitiesModel(equities);
            ChartCompaniesEquities chart = new ChartCompaniesEquities();
            chart.chartCompaniesEquities = equities;
            chart.chartGainers = gainers;
            chart.chartLosers = losers;

            return View("Recommendation", chart);
        }

        /****
         * Deletes the records from tables.
        ****/
        public void ClearTables(string tableToDel)
        {
            if ("all".Equals(tableToDel))
            {
                //First remove equities and then the companies
                dbContext.Equities.RemoveRange(dbContext.Equities);
                dbContext.RecommendationEquities.RemoveRange(dbContext.RecommendationEquities);
                dbContext.Companies.RemoveRange(dbContext.Companies);
            }
            else if ("Companies".Equals(tableToDel))
            {
                //Remove only those that don't have Equity stored in the Equitites table
                dbContext.Companies.RemoveRange(dbContext.Companies
                                                         .Where(c => c.Equities.Count == 0 && c.RecommendationEquities.Count == 0)
                                                                      );
            }
            else if ("Charts".Equals(tableToDel))
            {
                dbContext.Equities.RemoveRange(dbContext.Equities);
            }
            else if ("Recommendation".Equals(tableToDel))
            {
                dbContext.RecommendationEquities.RemoveRange(dbContext.RecommendationEquities);
                companiesEquities = null;
            }
            dbContext.SaveChanges();
        }

        /****
         * Returns the ViewModel CompaniesEquities based on the data provided.
         ****/
        public CompaniesEquities getCompaniesEquitiesModel(List<Equity> equities)
        {
            List<Company> companies = dbContext.Companies.ToList();

            if (equities.Count == 0)
            {
                return new CompaniesEquities(companies, null, "", "", "", 0, 0);
            }

            Equity current = equities.Last();
            string dates = string.Join(",", equities.Select(e => e.date));
            string prices = string.Join(",", equities.Select(e => e.high));
            string volumes = string.Join(",", equities.Select(e => e.volume / 1000000)); //Divide vol by million
            float avgprice = equities.Average(e => e.high);
            double avgvol = equities.Average(e => e.volume) / 1000000; //Divide volume by million
            return new CompaniesEquities(companies, equities.Last(), dates, prices, volumes, avgprice, avgvol);
        }

        /****
      * Returns the ViewModel CompaniesEquities based on the data provided.
      * Recommendation Property is set based on the HighPrice, LowPrice and Average Price
      ****/
       /* Stock Picking Strategy:

        Weekly stock recommendation is based on the stock’s performance for the past one month.The implementation details are the following:

        1.	Pick each stock, calculate the highest price for that month and the lowest price for that month
        2.	Calculate the average high and low price for that month
        3.	If the average price is close to the Highest Price, we have recommended that stock for buying.
        4.	If the average price is close to the low price, we have recommended the stock to be sold
            This indicates whether the stock is trending UP or DOWN to make the appropriate recommendations.
        5.	Once we have all the stocks, the order of the recommendation is decided by the average price for the last month.*/
        public CompaniesEquities getRecommendationCompaniesEquitiesModel(List<Equity> equities)
        {
            List<Company> companies = dbContext.Companies.ToList();

            if (equities.Count == 0)
            {
                return new CompaniesEquities(companies, null, "", "", "", 0, 0, 0, 0, "");
            }

            Equity current = equities.Last();
            string dates = string.Join(",", equities.Select(e => e.date));
            string prices = string.Join(",", equities.Select(e => e.high));
            string volumes = string.Join(",", equities.Select(e => e.volume / 1000000)); //Divide vol by million
            float avgprice = equities.Average(e => e.high);
            double avgvol = equities.Average(e => e.volume) / 1000000; //Divide volume by million

            //Find the maximum high value from the equities list and assign it to the highPrice
            float highPrice = equities.Max(e => e.high);
            //Find the minimum high value from the equities list and assign it to the lowPrice
            float lowPrice = equities.Where(e => e.high > 0).Min(e => e.high);
            string recommendation = "";
            //Setting the Recommendation based on the average price and highPrice
            if ((highPrice - avgprice) < (avgprice - lowPrice))
            {
                recommendation = "Buy";
            }
            else
            {
                recommendation = "Sell";
            }
            //float close = equities.
            float changePercent = ((highPrice - lowPrice) / lowPrice)*100;
            return new CompaniesEquities(companies, equities.Last(), dates, prices, volumes, avgprice, avgvol, highPrice, 
                lowPrice, recommendation);
        }

    }
}
