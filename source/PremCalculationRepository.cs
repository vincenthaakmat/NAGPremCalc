#region Using directives
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using Inspro.DataAccessLayer.SqlClient;
using Inspro.Utils;
using Inspro.BusinessModel;
using Libraries;
using System.ComponentModel;
using System.Windows.Forms;
#endregion

namespace Inspro.BusinessRules
{

	public partial class PremCalculationRepository
	{
		#region "Declarations"
		
		private static volatile PremCalculationRepository current;
		private static object syncRoot = new Object();
		private decimal rateIncrease = 0m;
		private PremiumRequest req;
		private DataSet DSRateIncrease = null;
		#endregion "Declarations"

		#region "Constructor"
		/// <summary>
		/// Creates a new <see cref="AuthorRepositoryBase"/> instance.
		/// Uses connection string to connect to datasource.
		/// </summary>
		/// <param name="connectionString">Connection string.</param>
		protected PremCalculationRepository()
		{
		}

		#endregion "Constructor"

		#region Public properties

		public static PremCalculationRepository Current
		{
			get
			{
				if (current == null)
				{
					lock (syncRoot)
					{
						if (current == null)
						{
							current = new PremCalculationRepository();
						}
					}
				}
				return current;
			}
		}

		#endregion Public properties

		#region Premium Calculation

		#region "GetPremiumRate"
		public PremiumResponse GetPremiumRate(PremiumRequest request)
		{
			PremiumResponse response = null;

			try
			{
				switch (request.Territory)
				{
					case "ANG":
						response = GetPremRateANG(request);
						break;

					case "ARU":
						response = GetPremRateABC(request);
						break;

					case "BAH":
						response = CalcNoOverride(request, true);
						break;

					case "BON":
						response = GetPremRateABC(request);
						break;

					case "BVI":
						response = GetPremRateBVI(request);
						break;

					case "CUR":
						response = GetPremRateABC(request);
						break;

					case "DOM":
						response = GetPremRateDOM(request);
						break;

					case "EUX":
						response = GetPremRateSEM(request);
						break;

					case "GRE":
						response = GetPremRateGRE(request);
						break;

					case "MON":
						response = GetPremRateSEM(request);
						break;

					case "NEV":
					case "SKT":
						response = GetPremRateSKT_NEV(request);
						break;

					case "SAB":
						response = GetPremRateSEM(request);
						break;

					case "SLU":
						response = GetPremRateSLU(request);
						break;

					case "SMD":
						response = GetPremRateSMD(request);
						break;

					case "SMF":
						response = GetPremRateSMF(request);
						break;

					case "SVC":
						response = GetPremRateSVC(request);
						break;

					case "TCI":
						response = GetPremRateTCI(request);
						break;
				}
			}
			catch (Exception exception)
			{
				ErrorLogAdapter.Current.LogError(exception);
			}
			return response;
		}

		#endregion "GetPremiumRate"

		#region "GetPremRateSMD | Sint Maarten"
		private PremiumResponse GetPremRateSMD(PremiumRequest request)
		{
			req = request;
			CompRate premium = null;
			PremVar premVar = null;
			ThirdPRate tppremium = null;
			PremiumResponse response = new PremiumResponse();
			decimal vehvalue = request.VehicleValue;
			decimal charge = 0;
			string coverage;
			decimal tmpBasicPremium = 0;
			int vehiclevalue = Convert.ToInt32(request.VehicleValue);

			try
			{
				coverage = request.Coverage;
				if (coverage == "TF") coverage = "C";
				List<Sys> sysList = SysRepository.Current.Find("CountryCode like '%" + request.Territory + "%'", "CountryCode");
				premVar = PremVarRepository.Current.GetByIDCoverageTerritory(request.VehicleUse, coverage, request.Territory)[0];

				if (request.NoOverride != true)
				{

					if (request.Coverage == "C" || request.Coverage == "TF")
					{
						if (request.Rental == false)
						{
							if (vehvalue > 10000)
							{
								request.VehicleValue = 10000;

								//This is to round the value of the vehicle to the next 1000
								int res = 0;
								int newVehValue = 0;

								System.Math.DivRem(Convert.ToInt32(vehvalue), 1000, out res);

								if (res > 0)
								{
									newVehValue = (Convert.ToInt32(vehvalue) - res) + 1000;
								}
								else
								{
									newVehValue = Convert.ToInt32(vehvalue);
								}

								charge = System.Math.Round((Convert.ToDecimal(newVehValue) - 10000) / 1000, MidpointRounding.AwayFromZero) * premVar.Charge;

							}
							premium = CompRateRepository.Current.GetCompRate(Convert.ToInt32(request.VehicleValue), request.CurrCode, request.VehicleUse, request.Territory)[0];

							if ((request.AgeCat <= 30 && request.InsuredSex == Gender.M) || ((request.AddDriver && request.AgeAddedDriver < 30)))
							{
								response.BasicPremium = premium.Under30;
							}
							else
							{
								response.BasicPremium = premium.Over30;
							}

							response.BasicPremium += charge;

							if (request.Coverage == "TF")
							{
								response.BasicPremium = Math.Round(response.BasicPremium * System.Convert.ToDecimal(75 / 100), 2, MidpointRounding.AwayFromZero);
							}
						}
						else
						{
							response.BasicPremium = Math.Round(vehiclevalue * System.Convert.ToDecimal(premVar.Rentalperc / 100), 2, MidpointRounding.AwayFromZero);
							if (request.Territory == "SMF") response.BasicPremium += premVar.Rentalvalue;
						}


					}
					else if (request.Coverage == "T")
					{
						if (request.Rental == false)
						{

							tppremium = ThirdPRateRepository.Current.GetByCurrCodeTerritoryID(request.CurrCode, request.Territory, request.VehicleUse)[0];

							if (request.EngineSize == 1601)  //this is for engine sizes => 1600 CC and non MC
								if (request.AgeCat <= 30 && request.InsuredSex == Gender.M) response.BasicPremium = tppremium.Under30over1600cc; else response.BasicPremium = tppremium.Over30over1600cc;
							else if (request.EngineSize == 1599) //this is for engine sizes < 1600 CC and non MC
								if (request.AgeCat <= 30 && request.InsuredSex == Gender.M) response.BasicPremium = tppremium.Under30under1600cc; else response.BasicPremium = tppremium.Over30under1600cc;
							else if (request.EngineSize == 51) response.BasicPremium = tppremium.Mcover50cc;
							else if (request.EngineSize == 49) response.BasicPremium = tppremium.Mcunder50cc;
						}
						else
						{
							response.BasicPremium = premVar.Rentalvalue;
						}

					}

				}
				else
				{
					response.BasicPremium = request.BasicPremium;
				}

				//Added on 23-Oct-2023 By Vincent
				// There will be a premium increase as per 1 Nov for NEW policies and 1-Jan for REN/APR Policies
				DSRateIncrease =  Getdataset();
				if (DSRateIncrease != null)
				{
					rateIncrease = Convert.ToDecimal(DSRateIncrease.Tables[0].Rows[0]["Rate"].ToString());
					//only use the new premium if the policy had its full year
					if (request.NCDDate == null) request.NCDDate = DateTime.Today;
					if (request.EffectiveDate == null) request.EffectiveDate = DateTime.Today;
					if (request.ApplicationDate == null) request.ApplicationDate = request.EffectiveDate;
					if (request.VehicleType == "MotorCycle" || request.VehicleUse == "MC")
					{
						response.BasicPremium = response.BasicPremium;
					}
					else if (request.NoOverride == true)
					{
						response.BasicPremium = request.BasicPremium;
					}
					else if (request.ApplicationDate > Convert.ToDateTime("11/15/2023") && request.IsNew == false)
					{
						response.BasicPremium = response.BasicPremium += response.BasicPremium * rateIncrease / 100;
					}
					else
                    {
						response.BasicPremium = response.BasicPremium += response.BasicPremium * rateIncrease / 100;
					}
				
					//else if (request.NCDDate > request.EffectiveDate && request.IsNew == false && request.PolType == "V")
					//{
					//	response.BasicPremium = response.BasicPremium;
					//}
					//else if (request.NCDDate > request.EffectiveDate && request.PolType == "V")
					//{
					//	response.BasicPremium = response.BasicPremium;
					//}
					//else if (request.ApplicationDate > Convert.ToDateTime("11/15/2023"))
					//{
					//	response.BasicPremium = response.BasicPremium += response.BasicPremium * rateIncrease / 100;
					//}
					
					//}
				}

				//Added by Vincent 14  Jan 2008
				// Deal Correctly with Rateup
				if (request.RateUp > 0)
				{
					response.RateUpAmt = Math.Round(response.BasicPremium * System.Convert.ToDecimal(request.RateUp / 100), 2, MidpointRounding.AwayFromZero);
					response.RateUP = Convert.ToDecimal(request.RateUp);
					//response.BasicPremium = Math.Round(response.BasicPremium + response.RateUpAmt, 2, MidpointRounding.AwayFromZero);
					tmpBasicPremium = Math.Round(response.BasicPremium + response.RateUpAmt, 2, MidpointRounding.AwayFromZero);
				}
				else
				{
					tmpBasicPremium = Math.Round(response.BasicPremium, 2, MidpointRounding.AwayFromZero);
				}


				//Added on 13 Nov 2015
				//New Calc of NVD, needs to be taken off from the basic premium
				if (request.NewVehDiscount && request.DontOverrideNVD == false)
				{
					response.NewVehDiscPerc = (100 - (sysList[0].Newvehdisc * 100)) * -1;
					response.NewVehDiscValue = (tmpBasicPremium - (Math.Round(tmpBasicPremium * sysList[0].Newvehdisc, 2, MidpointRounding.AwayFromZero))) * -1;
					//response.RateUP = response.NewVehDiscPerc;
					//response.RateUpAmt = response.NewVehDiscValue;
					tmpBasicPremium = Math.Round(tmpBasicPremium + response.NewVehDiscValue, 2, MidpointRounding.AwayFromZero);
				}


				if (request.Rental == false)
				{
					switch (request.VehicleUse)
					{
						case "PR": //Private
							if (request.DriverExp < 2) request.AALD = true;
							break;
						case "BS":  //Bus
						case "SB":  //School Bus
						case "CP":  //Commercial Private
						case "HD":  //Heavy Duty
						case "TX":  //Taxi
						case "RN":  //Car Rental
						case "GL":  //Group Tours Large
						case "GS":  //Group Tours Small
							request.AALD = true; //If vehicle use is commercial then AALD is automatically set to true
							break;
					}
					//Any Auth Driver and Experienced Driver
					if (request.AALD && request.DriverExp > 2)
					{
						response.AALD = Math.Round(tmpBasicPremium * System.Convert.ToDecimal(premVar.Aald / 100), 2, MidpointRounding.AwayFromZero);
						tmpBasicPremium = Math.Round(tmpBasicPremium + response.AALD, 2, MidpointRounding.AwayFromZero);
					}
					else
					{
						//Inexperienced Driver
						if (request.DriverExp < 2) response.LicenseExp = Math.Round(tmpBasicPremium * System.Convert.ToDecimal(premVar.Licexp / 100), 2, MidpointRounding.AwayFromZero);
						tmpBasicPremium = Math.Round(tmpBasicPremium + response.LicenseExp, 2, MidpointRounding.AwayFromZero);
					}
					//Foreign Licensed Driver
					if (request.ForLicense)
					{
						response.ForLicense = Math.Round(tmpBasicPremium * System.Convert.ToDecimal(premVar.Forlicense / 100), 2, MidpointRounding.AwayFromZero);
						tmpBasicPremium = Math.Round(tmpBasicPremium + response.ForLicense, 2, MidpointRounding.AwayFromZero);
					}
				}

				//Changed on Jan 23rd 2005 //No longer included in Rental if statement
				response.AddTPL = request.AddTPL;
				response.ToolsOfTrade = request.ToolsOfTrade;
				tmpBasicPremium = Math.Round(tmpBasicPremium + response.AddTPL + response.ToolsOfTrade, 2, MidpointRounding.AwayFromZero);

				List<Coverage> covlist = null;
				int maxncd = 0;
				covlist = CoverageController.Find("CoverageCode = '" + request.Coverage + "' AND Territory like '%" + request.Territory + "%'", "CoverageCode");
				if (covlist != null)
				{
					maxncd = covlist[0].Maxncd;
				}
				//if the client has a fleet discount, than that also needs to be added to be added to total discount calculation
				if (request.FleetDiscount)
				{
					decimal totaldisc = Convert.ToDecimal(request.NCD) + Convert.ToDecimal(premVar.Fleetdisc);
					if (totaldisc > maxncd)
					{
						request.FleetDiscount = false;
						response.Fleetdisc = 0m;
					}
					else
					{
						response.Fleetdisc = Math.Round(tmpBasicPremium * System.Convert.ToDecimal(premVar.Fleetdisc / 100), 2, MidpointRounding.AwayFromZero);
						tmpBasicPremium = Math.Round(tmpBasicPremium - response.Fleetdisc, 2, MidpointRounding.AwayFromZero);
					}
				}

				if (request.StaffDiscount)
				{
					if ( request.AgentCode == "EMP") // Direct NAGICO Staff
					{
						response.StaffDisc = Math.Round(tmpBasicPremium * System.Convert.ToDecimal(premVar.Staffdisc / 100), 2, MidpointRounding.AwayFromZero);
					}
					else
					{
						premVar.AgentStaffdisc = 20; //For now 20%, untill the database is updated to hold the value
						response.StaffDisc = Math.Round(tmpBasicPremium * System.Convert.ToDecimal(premVar.AgentStaffdisc / 100), 2, MidpointRounding.AwayFromZero);
					}
					tmpBasicPremium = Math.Round(tmpBasicPremium - response.StaffDisc, 2, MidpointRounding.AwayFromZero);
				}

				if (request.ManagDisc > 0)
				{
					response.ManagDisc = Math.Round(tmpBasicPremium * System.Convert.ToDecimal(request.ManagDisc / 100), 2, MidpointRounding.AwayFromZero);
					tmpBasicPremium = Math.Round(tmpBasicPremium - response.ManagDisc, 2, MidpointRounding.AwayFromZero);
				}

				response.YearPremium = tmpBasicPremium;
				response.GrossPremium = response.YearPremium;

				if (request.Period < 12 && request.ShortPeriod == false)
				{
					response.GrossPremium = Math.Round(response.YearPremium * System.Convert.ToDecimal(request.RateCode / 100), 2, MidpointRounding.AwayFromZero);
					switch (request.PremiumRateCode)
					{
						case "1Q":      //Quarterly rate
							response.GrossPremium = Math.Round(response.YearPremium * System.Convert.ToDecimal(premVar.Quarterlyperc / 100), 2, MidpointRounding.AwayFromZero);
							break;
						case "3M50":    // 1st 3 Months coverage
							response.GrossPremium = Math.Round(response.YearPremium / 2, 2, MidpointRounding.AwayFromZero);
							break;
						case "6M":      // 6 Month rate
							response.GrossPremium = Math.Round(response.YearPremium * System.Convert.ToDecimal(premVar.Halfyearlyperc / 100), 2, MidpointRounding.AwayFromZero);
							break;
						case "9M50":    //9 Months coverage (2nd part of the 3months coverage rate)
							response.GrossPremium = Math.Round(response.YearPremium / 2, 2, MidpointRounding.AwayFromZero);
							break;
					}
				}
				// add001-begin added by Vincent for short term calculation
				if (request.ShortPeriod)
				{
					response.GrossPremium = Math.Round(response.YearPremium * System.Convert.ToDecimal(request.ShortPeriod_Perc / 100), 2, MidpointRounding.AwayFromZero);
				}
				// add001-end end of added line
				


				if (request.NCD != 0) response.NCD = Math.Round(response.GrossPremium * System.Convert.ToDecimal(request.NCD / 100), 2, MidpointRounding.AwayFromZero);

				response.DiscountPremium = Math.Round(response.GrossPremium - response.NCD, 2, MidpointRounding.AwayFromZero);

				
				// Changed by Vincent 16 Oct 2012
				// Nagico gives an extra 30% discount on New Vehicles
				// and Liability of Naf 200,000.00 without extra charge if the NCD is maximum NCD
				// Total Discount can't be more than 60% (Maximum discount for that coverage

				//Check first what the Max NCD for this coverage is

				//Make sure the default NVD value is in the response 19-Nov-2012
				//response.NewVehDiscPerc = request.NewVehDiscPerc;
				//response.NewVehDiscValue = request.NewVehDiscValue; 
				//--------------------------------------------------
				if (request.NewVehDiscount && request.DontOverrideNVD == false)
				{
					//response.NewVehDiscValue = 0;
					//response.NewVehDiscPerc = 0;
					//if the client has a fleetdiscount, than that also needs to be added to be added to total discount calculation
					//if (request.FleetDiscount == true)
					//{
					//    decimal totaldisc =  Convert.ToDecimal(request.NCD) + Convert.ToDecimal(premVar.Fleetdisc) ; 
					//    //if (100 - (sysList[0].Newvehdisc * 100) + Convert.ToDecimal(request.NCD) + Convert.ToDecimal(premVar.Fleetdisc) <= maxncd)
					//    if (totaldisc > maxncd)
					//    {
					//        request.FleetDiscount = false;
					//    }
					//    else
					//    {
					//        response.NewVehDiscPerc = maxncd - request.NCD -  Convert.ToDecimal(premVar.Fleetdisc);
					//        if (response.NewVehDiscPerc < 0) response.NewVehDiscPerc = 0;
					//        response.NewVehDiscValue = (Math.Round(response.DiscountPremium * (response.NewVehDiscPerc/100), 2, MidpointRounding.AwayFromZero));
					//    }
					//}
					//else
					//{

					//   // if (100 - (sysList[0].Newvehdisc * 100) + Convert.ToDecimal(request.NCD) <= maxncd)
					//    decimal maxnvd = (100 - (sysList[0].Newvehdisc * 100) + Convert.ToDecimal(request.NCD));
					//    if (maxnvd <= maxncd)
					//    {
					//        response.NewVehDiscPerc = 100 - (sysList[0].Newvehdisc * 100);
					//        response.NewVehDiscValue = response.DiscountPremium - (Math.Round(response.DiscountPremium * sysList[0].Newvehdisc, 2, MidpointRounding.AwayFromZero));
					//    }
					//    else
					//    {
					//        response.NewVehDiscPerc = maxncd - request.NCD;
					//        if (response.NewVehDiscPerc < 0) response.NewVehDiscPerc = 0;
					//        response.NewVehDiscValue = (Math.Round(response.DiscountPremium * (response.NewVehDiscPerc/100), 2, MidpointRounding.AwayFromZero));
					//    }
					//}
				}

				// Added by Vincent on Mar 28, 2013
				//if override is checked, use the value that is entered
				decimal NewVehDiscValue = 0;
				if (request.DontOverrideNVD)
				{
					response.NewVehDiscPerc = request.NVDPerc;
					//response.NewVehDiscValue = response.DiscountPremium - (Math.Round(response.DiscountPremium * (request.NVDPerc/100), 2, MidpointRounding.AwayFromZero));
					response.NewVehDiscValue = (Math.Round((response.DiscountPremium * (request.NVDPerc / 100)), 2, MidpointRounding.AwayFromZero));
					NewVehDiscValue = response.NewVehDiscValue;
				}

				//_______________________________________________________________

				if (request.PassLiab > 0)
				{
					response.PassLiab = Math.Round(System.Convert.ToDecimal(request.PassLiab * premVar.Passliab), 2, MidpointRounding.AwayFromZero);
					if (request.ShortPeriod == false)
					{
						switch (request.PremiumRateCode)
						{
							case "1Q":
								response.PassLiab = Math.Round((response.PassLiab * System.Convert.ToDecimal(premVar.Quarterlyperc / 100)), 2, MidpointRounding.AwayFromZero);
								break;
							case "3M50":
								response.PassLiab = Math.Round(response.PassLiab / 2, 2, MidpointRounding.AwayFromZero);
								break;
							case "6M":
								response.PassLiab = Math.Round((response.PassLiab * System.Convert.ToDecimal(premVar.Halfyearlyperc / 100)), 2, MidpointRounding.AwayFromZero);
								break;
							case "9M50":
								response.PassLiab = Math.Round(response.PassLiab / 2, 2, MidpointRounding.AwayFromZero);
								break;
						}
					}
					else
					{
						response.PassLiab = Math.Round((response.PassLiab * System.Convert.ToDecimal(request.ShortPeriod_Perc / 100)), 2, MidpointRounding.AwayFromZero);
					}
				}

				if (request.isPercentageCampaign) response.CampaignAmt = ((response.DiscountPremium + response.PassLiab + request.ExtraCoverage1 + request.ExtraCoverage2) * request.Campaigns) / 100; else response.CampaignAmt = request.Campaigns;

				response.NetPremium = Math.Round(response.DiscountPremium - NewVehDiscValue + response.PassLiab + request.ExtraCoverage1 + request.ExtraCoverage2 + response.CampaignAmt, 2, MidpointRounding.AwayFromZero);
				//response.NetPremium = Math.Round(response.DiscountPremium  + response.PassLiab + request.ExtraCoverage1 + request.ExtraCoverage2 + response.CampaignAmt, 2, MidpointRounding.AwayFromZero);
				response.PolicyFee = request.Policyfee;
				response.TotalPremium = Math.Round(response.NetPremium, 2, MidpointRounding.AwayFromZero) + response.PolicyFee + Math.Round(response.GovTax, 2, MidpointRounding.AwayFromZero);

			}

			catch (Exception exception)
			{
				ErrorLogAdapter.Current.LogError(exception);
			}
			return response;
		}
		#endregion "GetPremRateSMD"

		#region "GetPremRateSMF | Saint Martin"
		private PremiumResponse GetPremRateSMF(PremiumRequest request)
		{
			CompRate premium = null;
			PremVar premVar = null;
			ThirdPRate tppremium = null;
			PremiumResponse response = new PremiumResponse();
			decimal vehvalue = request.VehicleValue;
			decimal charge = 0;
			string coverage;
			decimal tmpBasicPremium = 0;
			decimal terrorism = Convert.ToDecimal(3.3);

			//int vehiclevalue = int.Parse(request.VehicleValue.ToString());

			int vehiclevalue = Convert.ToInt32(request.VehicleValue);

			try
			{
				//coverage = request.Coverage;
				//if (coverage == "TF") coverage = "C";

				coverage = request.Coverage;
				switch (request.Coverage)
				{
					case "TFW":
					case "TW":
					case "TF":
					case "C":
						coverage = "C";
						break;

					case "T":
						coverage = "T";
						break;
					default:
						break;
				}

				List<Sys> sysList = null;
				if (request.SysList == null)
				{
					sysList = SysRepository.Current.Find("CountryCode = '" + request.Territory + "'", "CountryCode");
				}
				else
				{
					sysList = request.SysList;
				}

				if (request.Premvar == null)
				{
					premVar = PremVarRepository.Current.Find("Territory = '" + request.Territory + "' and Coverage = '" + coverage + "' and Id = '" + request.VehicleUse + "'", "Territory")[0];
				}
				else
				{
					premVar = request.Premvar;
				}
				//ThirdPRate tpRate = GenericBusinessLogicLayer<ThirdPRate>.Find("Territory = \"" + request.Territory + "\" and Id = \"" + request.VehicleUse + "\"", "Territory")[0];

				//request.Policyfee = Convert.ToDecimal(sysList[0].Policyfee);

				if (request.NoOverride != true)
				{

					//premVar = PremVarRepository.Current.GetByIDCoverageTerritory(request.VehicleUse, coverage, request.Territory)[0];

					if (coverage == "C")
					{
						if (request.Rental == false)
						{
							if (vehvalue > 10000)
							{
								request.VehicleValue = 10000;

								//This is to round the value of the vehicle to the next 1000
								int res = 0;
								int newVehValue = 0;

								System.Math.DivRem(Convert.ToInt32(vehvalue), 1000, out res);

								if (res > 0)
								{
									newVehValue = (Convert.ToInt32(vehvalue) - res) + 1000;
								}
								else
								{
									newVehValue = Convert.ToInt32(vehvalue);
								}

								charge = System.Math.Round((Convert.ToDecimal(newVehValue) - 10000) / 1000, MidpointRounding.AwayFromZero) * Convert.ToDecimal(premVar.Charge);

							}
							//premium = CompRateRepository.Current.GetCompRate(Convert.ToInt32(request.VehicleValue), request.CurrCode, request.VehicleUse, request.Territory)[0];
							premium = CompRateRepository.Current.Find("Territory = '" + request.Territory + "' and Id = '" + request.VehicleUse + "' and " + request.VehicleValue + " >= Frsumins and " + request.VehicleValue + " <= Tosumins", "Territory")[0];
							//()


							if ((request.AgeCat <= 25 && request.InsuredSex == Gender.M) || ((request.AddDriver && request.AgeAddedDriver < 25)))
							{
								response.BasicPremium = Convert.ToDecimal(premium.Under30);
							}
							else
							{
								response.BasicPremium = Convert.ToDecimal(premium.Over30);
							}

							//if (request.VehicleValue > 8000)
							//{
							response.BasicPremium += charge;

							decimal tpPrem = GetTPPremium(request, premVar);

							decimal compNoTP = response.BasicPremium - tpPrem;
							decimal compBase = 0;
							double compBaseRate = 1;

							//if (request.Puissance < 6)
							//{
							compBaseRate = 0.7;
							//}
							//else if (request.Puissance >= 6 && request.Puissance <= 11)
							//{
							//    compBaseRate = 0.8;
							//}
							//else if (request.Puissance > 11)
							//{
							//    compBaseRate = 1.25;
							//}
							compBase = compNoTP * Convert.ToDecimal(compBaseRate);

							//response.BasicPremium = compBase;
							//}

							//response.BasicPremium = response.BasicPremium - (response.BasicPremium * Convert.ToDecimal(0.156)); //To absorb a percentage of the tax

							//if (request.Coverage == "TF")
							//{
							//    response.BasicPremium = Math.Round(response.BasicPremium * System.Convert.ToDecimal(75 / 100), 2);
							//}

							switch (request.Coverage)
							{
								////case "F1":
								////    response.BasicPremium = compBase * Convert.ToDecimal(1) + tpPrem;
								////    break;
								case "TF":
									response.BasicPremium = compBase * Convert.ToDecimal(0.1) + tpPrem;
									break;
								case "TW":
									response.BasicPremium = compBase * Convert.ToDecimal(0.45) + tpPrem;
									//response.BasicPremium = response.BasicPremium * Convert.ToDecimal(0.6);
									break;
								case "TFW":
									response.BasicPremium = compBase * Convert.ToDecimal(0.7) + tpPrem;
									//response.BasicPremium = response.BasicPremium * Convert.ToDecimal(0.7);
									break;
								case "C":
									response.BasicPremium = compBase * Convert.ToDecimal(1) + tpPrem;
									break;
								default:
									break;
							}
						}
						else
						{
							response.BasicPremium = Math.Round(vehiclevalue * System.Convert.ToDecimal(premVar.Rentalperc / 100), 2);
							if (request.Territory == "SMF") response.BasicPremium += Convert.ToDecimal(premVar.Rentalvalue);
						}

						//if (request.Coverage == "F3")
						//{
						//    response.BasicPremium = Math.Round(response.BasicPremium * System.Convert.ToDecimal(75 / 100), 2);
						//}



					}
					else if (coverage == "T")
					{
						response.BasicPremium = GetTPPremium(request, premVar);
					}
				}
				else
				{
					response.BasicPremium = request.BasicPremium;
				}

				//Added by Vincent 18 April 2006
				// Nagico gives an extra 30% discount on New Vehicles
				// and Liability of Naf 200,000.00 without extra charge
				if (request.NewVehDiscount)
				{
					response.BasicPremium = Math.Round(response.BasicPremium * Convert.ToDecimal(sysList[0].Newvehdisc), 2);
				}
				//Added by Vincent 14  Jan 2008
				// Deal Correctly with Rateup
				//response.RateUP = Math.Round(request.BasicPremium * System.Convert.ToDecimal(request.RateUp / 100), 2);
				response.RateUP = Math.Round(response.BasicPremium * System.Convert.ToDecimal(request.RateUp / 100), 2);
				response.RateUpAmt = response.RateUP;
				tmpBasicPremium = Math.Round(response.BasicPremium + response.RateUpAmt, 2);

				if (request.AddRefPrem == false)
				{
					if (request.Rental == false)
					{
						switch (request.VehicleUse)
						{
							case "PR":
								if (request.DriverExp < 2) request.AALD = true;
								break;
							case "BS":
							case "SB":
							case "CP":
							case "HD":
							case "TX":
							case "RN":
							case "GL":
							case "GS":
								request.AALD = true; //If vehicle use is commercial then AALD is automatically set to true
								break;
						}
						if (request.AALD && request.DriverExp > 2)
						{
							response.AALD = Math.Round(tmpBasicPremium * System.Convert.ToDecimal(premVar.Aald / 100), 2);
							tmpBasicPremium = Math.Round(tmpBasicPremium + response.AALD, 2);
						}
						else
						{
							if (request.DriverExp < 2) response.LicenseExp = Math.Round(tmpBasicPremium * System.Convert.ToDecimal(premVar.Licexp / 100), 2);
							tmpBasicPremium = Math.Round(tmpBasicPremium + response.LicenseExp, 2);
						}
						if (request.ForLicense)
						{
							response.ForLicense = Math.Round(tmpBasicPremium * System.Convert.ToDecimal(premVar.Forlicense / 100), 2);
							tmpBasicPremium = Math.Round(tmpBasicPremium + response.ForLicense, 2);
						}
					}
				}

				//Changed on Jan 23rd 2005 //No longer included in Rental if statement
				response.AddTPL = request.AddTPL;
				response.ToolsOfTrade = request.ToolsOfTrade;
				tmpBasicPremium = Math.Round(tmpBasicPremium + response.AddTPL + response.ToolsOfTrade, 2);

				//if (request.Rental == true) response.Rental = tmpBasicPremium * System.Convert.ToDecimal(premVar.RentalPerc / 100) + premVar.RentalValue;
				if (request.FleetDiscount)
				{
					response.Fleetdisc = Math.Round(tmpBasicPremium * System.Convert.ToDecimal(premVar.Fleetdisc / 100), 2);
					tmpBasicPremium = Math.Round(tmpBasicPremium - response.Fleetdisc, 2);
				}
				if (request.StaffDiscount)
				{
					response.StaffDisc = Math.Round(tmpBasicPremium * System.Convert.ToDecimal(premVar.Staffdisc / 100), 2);
					tmpBasicPremium = Math.Round(tmpBasicPremium - response.StaffDisc, 2);
				}


				if (request.ManagDisc > 0)
				{
					response.ManagDisc = Math.Round(tmpBasicPremium * System.Convert.ToDecimal(request.ManagDisc / 100), 2);
					tmpBasicPremium = Math.Round(tmpBasicPremium - response.ManagDisc, 2);
				}

				//response.YearPremium = tmpBasicPremium + response.LicenseExp + response.AALD + response.ForLicense - response.Fleetdisc - response.StaffDisc - response.ManagDisc + response.Rental + request.ToolsOfTrade + request.AddTPL ;  //+ response.AddDriver
				response.YearPremium = tmpBasicPremium;
				response.GrossPremium = response.YearPremium;

				if (request.Period < 12 && request.ShortPeriod == false)
				{
					//response.GrossPremium = Math.Round(response.YearPremium * System.Convert.ToDecimal(request.RateCode / 100), 2, MidpointRounding.AwayFromZero);
					if (request.RateCode != 0)
					{
						response.GrossPremium = Math.Round(response.YearPremium * System.Convert.ToDecimal(request.RateCode / 100), 2);
					}
					switch (request.PremiumRateCode)
					{
						case "1Q":
							response.GrossPremium = Math.Round(response.YearPremium * System.Convert.ToDecimal(premVar.Quarterlyperc / 100), 2);
							break;
						case "3M50":
							response.GrossPremium = Math.Round(response.YearPremium / 2, 2);
							break;
						case "6M":
							response.GrossPremium = Math.Round(response.YearPremium * System.Convert.ToDecimal(premVar.Halfyearlyperc / 100), 2);
							break;
						case "9M50":
							response.GrossPremium = Math.Round(response.YearPremium / 2, 2);
							break;
					}
				}
				// add001-begin added by Vincent for short term calculation
				if (request.ShortPeriod)
				{
					response.GrossPremium = Math.Round(response.YearPremium * System.Convert.ToDecimal(request.ShortPeriod_Perc / 100), 2);
				}
				// add001-end end of added line
				//Bonus Malus
				if (request.NCD != 0)
				{
					response.NCD = Math.Round(response.GrossPremium - (response.GrossPremium * System.Convert.ToDecimal(request.NCD)), 2);
				}

				response.DiscountPremium = Math.Round(response.GrossPremium - response.NCD, 2);
				if (request.PassLiab > 0)
				{
					response.PassLiab = Math.Round(System.Convert.ToDecimal(request.PassLiab * premVar.Passliab), 2);
					if (request.ShortPeriod == false)
					{
						switch (request.PremiumRateCode)
						{
							case "1Q":
								response.PassLiab = Math.Round((response.PassLiab * System.Convert.ToDecimal(premVar.Quarterlyperc / 100)), 2);
								break;
							case "3M50":
								response.PassLiab = Math.Round(response.PassLiab / 2, 2);
								break;
							case "6M":
								response.PassLiab = Math.Round((response.PassLiab * System.Convert.ToDecimal(premVar.Halfyearlyperc / 100)), 2);
								break;
							case "9M50":
								response.PassLiab = Math.Round(response.PassLiab / 2, 2);
								break;
						}
					}
					else
					{
						response.PassLiab = Math.Round((response.PassLiab * System.Convert.ToDecimal(request.ShortPeriod_Perc / 100)), 2);
					}
				}
				response.PolicyFee = request.Policyfee;
				response = CalcTax(request, premVar, response);
			}

			catch (Exception exception)
			{
				ErrorLogAdapter.Current.LogError(exception);
				//ExceptionManager.Publish(exception);
			}
			return response;
		}

		private static decimal GetTPPremium(PremiumRequest request, PremVar premVar)
		{
			ThirdPRate tppremium = null;
			decimal premium = 0;
			if (request.Rental == false)
			{
				if (request.Tppremium == null)
				{
					tppremium = ThirdPRateRepository.Current.Find("Territory = '" + request.Territory + "' and Id = '" + request.VehicleUse + "'", "Territory")[0];
				}
				else
				{
					tppremium = request.Tppremium;
				}

				if (request.EngineSize == 1601)  //this is for engine sizes => 1600 CC and non MC
					if (request.AgeCat <= 25 && request.InsuredSex == Gender.M) premium = Convert.ToDecimal(tppremium.Under30over1600cc); else premium = Convert.ToDecimal(tppremium.Over30over1600cc);
				if (request.EngineSize == 1599) //this is for engine sizes < 1600 CC and non MC
					if (request.AgeCat <= 25 && request.InsuredSex == Gender.M) premium = Convert.ToDecimal(tppremium.Under30under1600cc); else premium = Convert.ToDecimal(tppremium.Over30under1600cc);
				if (request.EngineSize == 51) premium = Convert.ToDecimal(tppremium.Mcover50cc);
				if (request.EngineSize == 49) premium = Convert.ToDecimal(tppremium.Mcunder50cc);
			}
			else
			{
				premium = Convert.ToDecimal(premVar.Rentalvalue);
			}
			return premium;
		}

		private static PremiumResponse CalcTax(PremiumRequest request, PremVar premvar, PremiumResponse resp)
		{

			decimal tpPremium = 0;

			switch (request.Coverage)
			{
				case "T":
					tpPremium = resp.DiscountPremium;
					CalculateTax(resp, request, premvar, tpPremium, request.Coverage);
					break;

				case "TF":
				case "TW":
				case "TFW":
				case "C":
					tpPremium = GetTPPremium(request, premvar);
					CalculateTax(resp, request, premvar, tpPremium, request.Coverage);

					break;
				default:
					break;
			}

			return resp;
		}

		private static void CalculateTax(PremiumResponse resp, PremiumRequest request, PremVar premvar, decimal tpPremium, string coverage)
		{
			double localTax = 0.1;
			double sst = 0.15;
			double catNat = 0.12;
			double gf = 0.012;
			double ter = 3.3;
			double localTGCA = 0.02;
			decimal govTax = 0;
			decimal govTaxComp = 0;
			decimal govTaxTPRC = 0;
			decimal govTaxTPOther = 0;
			decimal govTaxCatNat = 0;
			decimal rcGarantPremium = 0;
			decimal otherGaratPremium = 0;
			decimal compWithoutTP = 0;
			decimal drValue = 15;
			decimal pjValue = 35;
			decimal catNatValue = 0;
			decimal govTaxPolicyfee = 0;
			decimal govTaxPassLiab = 0;

			//if (request.RateCode != 0)
			//{
			//    tpPremium = Math.Round(tpPremium * System.Convert.ToDecimal(request.RateCode / 100), 2);
			//}

			switch (request.PremiumRateCode)
			{
				case "1Q":
					tpPremium = Math.Round((tpPremium * System.Convert.ToDecimal(premvar.Quarterlyperc / 100)), 2);
					break;
				case "3M50":
					tpPremium = Math.Round(tpPremium / 2, 2);
					break;
				case "6M":
					tpPremium = Math.Round((tpPremium * System.Convert.ToDecimal(premvar.Halfyearlyperc / 100)), 2);
					break;
				case "9M50":
					tpPremium = Math.Round(tpPremium / 2, 2);
					break;
			}

			switch (coverage)
			{
				case "T":
				case "TF":
				case "TW":
				case "TFW":
					break;

				case "C":
					compWithoutTP = resp.DiscountPremium - tpPremium;
					break;

				default:
					break;
			}

			//get RC and (DR/PJ) values
			otherGaratPremium = drValue + pjValue;
			rcGarantPremium = tpPremium - otherGaratPremium;

			switch (coverage)
			{
				case "T":
				case "TF":
				case "TW":
				case "TFW":
					break;

				case "C":
					//Calculate the CN value | it is already included in premium
					catNatValue = (compWithoutTP / (1 + Convert.ToDecimal(catNat))) * Convert.ToDecimal(catNat);
					break;

				default:
					break;
			}


			//Calculated taxes
			govTaxTPRC = rcGarantPremium * Convert.ToDecimal(localTax + sst + gf);
			govTaxTPOther = otherGaratPremium * Convert.ToDecimal(localTax);
			switch (coverage)
			{
				case "T":
				case "TF":
				case "TW":
				case "TFW":
					break;

				case "C":
					//Calculate the CN value | it is already included in premium
					govTaxComp = compWithoutTP * Convert.ToDecimal(localTax);
					break;

				default:
					break;
			}

			govTax = govTaxComp + govTaxTPRC + govTaxTPOther + govTaxCatNat + Convert.ToDecimal(ter);

			//Calculated policyfee tax
			govTaxPolicyfee = resp.PolicyFee * Convert.ToDecimal(localTGCA);

			//Calculate PassLiab
			govTaxPassLiab = resp.PassLiab * Convert.ToDecimal(localTax);

			resp.NetPremium = resp.DiscountPremium + resp.PassLiab;// +catNatValue;
			resp.CompWithoutTPPremium = compWithoutTP;
			resp.RcGarantPremium = rcGarantPremium;
			resp.OtherTPGarantPremium = otherGaratPremium;

			switch (coverage)
			{
				case "T":
				case "TF":
				case "TW":
				case "TFW":
					break;

				case "C":
					resp.CatNat = catNatValue;
					break;

				default:
					break;
			}
			resp.DefenseRecours = drValue;
			resp.ProtectionJuridique = pjValue;
			resp.GovTaxPolicyfee = Math.Round(govTaxPolicyfee, 2);
			resp.GovTaxComp = Math.Round(govTaxComp, 2);
			resp.GovTaxTPRC = Math.Round(govTaxTPRC, 2);
			resp.GovTaxTPOther = Math.Round(govTaxTPOther, 2);
			resp.GovTax = Math.Round(govTax, 2) + Math.Round(govTaxPassLiab, 2);
			resp.GovTaxTotal = Math.Round(govTax, 2);
			resp.Terrorism = Convert.ToDecimal(ter);

			resp.TotalPremium = Math.Round(resp.NetPremium, 2) + resp.PolicyFee + Math.Round(resp.GovTax, 2) + Math.Round(resp.GovTaxPolicyfee, 2);
		}
		#endregion "GetPremRateSMF"

		#region "GetPremRateABC | Aruba, Bonaire, Curaçao"
		public PremiumResponse GetPremRateABC(PremiumRequest request)
		{
			
			req = request;
			PremiumResponse response = new PremiumResponse();
			List<NVehUse> nVehUseList;
			PremVar premVar = null;
			string coverage = request.Coverage;
			decimal TPPrem = 0;
			//Grab the premium from abcTPRate table
			string where = "TOSUMINS >=" + System.Convert.ToDouble(request.VehicleValue) + " and Territory = '" + request.Territory + "'";
			if ((request.Territory == "ARU" || request.Territory == "CUR") && (request.VehicleUse == "HB"|| request.VehicleType == "MotorCycle"))
			{
				if (request.VehicleUse == "HB"||(request.VehicleType == "MotorCycle" && request.Coverage=="LAR"))
				{
					where = " Territory = '" + request.Territory + "'";
					where = where + " and VehCat = '" + request.Coverage + "'";
				}
				if (request.Coverage == "LAR")
                {
					where += "AND TOSUMINS >=" + System.Convert.ToDouble(request.VehicleValue) + " ";
				}
			}
			List<AbcTprate> abcTPRateList = ABCTPRateRepository.Current.Find(where, "TOSUMINS"); //, "MIN(Premium) as Premium");
			List<LiabilityVar> liabilityVarList = LiabilityVarRepository.Current.Find("Amount = '" + request.Liability + "' and Territory like '%" + request.Territory + "%'", "Amount");
			
			//-------------------------------------------------------------------------------
			TPPrem = abcTPRateList[0].Premium;
			if (request.NoOverride != true)
			{
				//Select First the Coverage
				switch (request.Coverage)
				{

					//Third Party Calculation
					case "TP":
					case "LAR": //Limited All Risk 
						response.BasicPremium = Convert.ToDecimal(abcTPRateList[0].Premium);
						nVehUseList = NVehUseRepository.Current.Find("VuseID='" + request.VehicleUse + "' and Territory = '" + request.Territory + "'", "VuseID");

						if (nVehUseList.Count > 0)
						{
							response.BasicPremium = response.BasicPremium + (response.BasicPremium * nVehUseList[0].Loadperc);
							//reuest on 2-June-2023 to increase TP premium by 10%
							//request to remove the 10% on June 6
							//if (request.Territory == "ARU") response.BasicPremium *= 1.10m;
						}
						break;
					
					case "TPC":
						List<Coverage> coverageList = CoverageRepository.Current.Find("CoverageCode='" + request.Coverage + "' and Territory = '" + request.Territory + "'", "CoverageCode");
						response.BasicPremium = request.VehicleValue * coverageList[0].Loadperc;

						//Add Premium loadup based on Vehicle Use
						nVehUseList = NVehUseRepository.Current.Find("VuseID='" + request.VehicleUse + "' and Territory = '" + request.Territory + "'", "VuseID");
						if (nVehUseList.Count > 0)
						{
							response.BasicPremium = response.BasicPremium + (response.BasicPremium * nVehUseList[0].Loadperc);
						}
						//reuest on 2-June-2023 to increase TPC premium by 5%
						if (request.Territory == "ARU" && request.IsNew) response.BasicPremium *= 1.05m;
						break;

					case "C":
					case "SC":
						//Other Coverages Calculation
						List<Coverage> coverageListTPC = CoverageRepository.Current.Find("CoverageCode='" + request.Coverage + "' and Territory = '" + request.Territory + "'", "CoverageCode");
						response.BasicPremium = request.VehicleValue * coverageListTPC[0].Loadperc;

						//Add Premium loadup based on Vehicle Use
						nVehUseList = NVehUseRepository.Current.Find("VuseID='" + request.VehicleUse + "' and Territory = '" + request.Territory + "'", "VuseID");
						if (nVehUseList.Count > 0)
						{
							response.BasicPremium = response.BasicPremium + (response.BasicPremium * nVehUseList[0].Loadperc);
						}

						//Added by Vincent on 3-Feb-2015
						//Rate up Super Coverage by 5% for BON & CUR, while ARU get 15%
						if (request.Coverage == "SC")
						{
							//As Of Jan ARU now is also 5%, but will be tken from the Coverage table Replacementvalue Field that isn't being used for the ABC islands
							response.BasicPremium = response.BasicPremium + (response.BasicPremium * coverageListTPC[0].Replacementvalue);
							//if (request.Territory == "ARU")
							//{
							//    response.BasicPremium *= 1.15m;
							//}
							//else
							//{
							//    response.BasicPremium *= 1.05m;
							//}
						}

						break;
				}

				//Added on 23-Oct-2023 By Vincent
				// There will be a premium increase as per 1 Nov for NEW policies and 1-Jan for REN/APR Policies
				DSRateIncrease = Getdataset();
				if (DSRateIncrease != null)
				{
					rateIncrease = Convert.ToDecimal(DSRateIncrease.Tables[0].Rows[0]["Rate"].ToString());
					response.BasicPremium += response.BasicPremium * rateIncrease / 100;
				}

				List<LiabilityVar> liabilityVarList1 = LiabilityVarRepository.Current.Find("Amount = '" + request.Liability + "' and Territory like '%" + request.Territory + "%'", "Amount");
				if (liabilityVarList1 != null && liabilityVarList1.Count > 0)
				{
					request.AddTPL = Convert.ToDecimal(liabilityVarList1[0].Value);
					response.AddTPL = response.BasicPremium * request.AddTPL;
				}
				//Add RateUp on top of the Basic Premium FOR ABC
				if (request.Territory == "CUR")
				{
					response.RateUP = Math.Round(response.BasicPremium * System.Convert.ToDecimal(request.RateUp / 100), 2, MidpointRounding.AwayFromZero);
					response.RateUpAmt = response.RateUP;
//					response.BasicPremium = Math.Round(response.BasicPremium + response.RateUpAmt, 2, MidpointRounding.AwayFromZero);
				}
				else
				{
					response.RateUP = 0;
				}
				request.BasicPremium = response.BasicPremium;
			}

			response = CalcNoOverride(request, true);
			return response;
		}
		#endregion "GetPremRateABC"

		#region "GetPremRateSEM | Saba, Statia, Montserrat"
		public PremiumResponse GetPremRateSEM(PremiumRequest request)
		{
			
				//'Application specific variables
				PremiumResponse response = new PremiumResponse();
				req = request;
			try
			{
				decimal vehvalue = request.VehicleValue;
				decimal charge = 0;
				decimal tmpBasicPremium = 0;
				if (request.NoOverride != true)
				{
					List<Sys> sysList = SysRepository.Current.Find("CountryCode like '%" + request.Territory + "%'", "CountryCode");

					//Select First the Coverage
					switch (request.Coverage)
					{
						//Third Party Calculation
						case "T":

							switch (request.Territory)
							{
								case "EUX":
								case "MON":
									response.BasicPremium = CalcTPMONEUX(request);
									break;

								case "SAB":
									string whereS = "VehUse = '" + request.VehicleUse + "' and CountryCode = '" + request.Territory + "'";
									whereS = whereS + " and Age = '" + request.AgeCat + "' and VehType = '" + request.VehicleType + "'";
									List<ThirdPRateSABA> tpRateList = ThirdPRateSABARepository.Current.Find(whereS, "TPRateID");
									if (tpRateList.Count == 0)
									{
										response.errorMessage = "Please select a proper Vehicle Type that corresponds with its use";
									}
									response.BasicPremium = Convert.ToDecimal(tpRateList[0].Amount);
									if (request.AgeCat < 26)
									{
										//response.BasicPremium = response.BasicPremium + response.BasicPremium * (tpRateList[0].Loadperc / 100);
									}
									break;
							}

							break;

						case "TF":
						case "C":
							switch (request.Territory)
							{
								case "MON":
									response.BasicPremium = CalcTPMONEUX(request);

									List<CompRateMON> compRMList = CompRateMONRepository.Current.Find("RateType ='" + request.VehicleUse + "'", "RateType");
									//Rentals are getting a different Calc.
									if (compRMList.Count == 0)
									{
										response.errorMessage = "Please select a proper Vehicle Type that corresponds with its use";
									}
									if (request.VehicleUse == "RN")
									{
										response.BasicPremium = vehvalue * (compRMList[0].Compover30perc / 100);
									}
									else
									{
										if (request.AgeCat == 31)
										{
											response.BasicPremium = response.BasicPremium + vehvalue * (compRMList[0].Compover30perc / 100);
										}
										else
										{
											response.BasicPremium = response.BasicPremium + vehvalue * (compRMList[0].Compunder30perc / 100);
										}
									}
									break;

								case "SAB":
								case "EUX":
									PremVar premVar = null;
									List<PremVar> prems = PremVarRepository.Current.GetByIDCoverageTerritory(request.VehicleUse, "C", request.Territory);
									if (prems.Count > 0)
									{
										premVar = prems[0];
									}
									else
									{
										return new PremiumResponse();
									}



									if (vehvalue > 20000)
									{
										request.VehicleValue = 20000;
									}
									string where = "RangeTo >=" + System.Convert.ToDouble(request.VehicleValue) + " and ObjectCategory = '" + request.VehicleUse + "' and CountryCode = '" + request.Territory + "'";
									List<CompRateSABEUX> compRSEList = CompRateSABEUXRepository.Current.Find(where, "CountryCode");
									if (compRSEList.Count == 0)
									{
										response.errorMessage = "Please select a proper Vehicle Type that corresponds with its use";
										return new PremiumResponse();
									}
									response.BasicPremium = compRSEList[0].Premiumamount;

									if (vehvalue > 20000)
									{
										//This is to round the value of the vehicle to the next 1000
										int res = 0;
										int newVehValue = 0;

										System.Math.DivRem(Convert.ToInt32(vehvalue), 1000, out res);

										if (res > 0)
										{
											newVehValue = (Convert.ToInt32(vehvalue) - res) + 1000;
										}
										else
										{
											newVehValue = Convert.ToInt32(vehvalue);
										}

										charge = System.Math.Round((Convert.ToDecimal(newVehValue) - 20000) / 1000, MidpointRounding.AwayFromZero) * premVar.Charge;

									}
									response.BasicPremium += charge;

									if (request.Rental)
									{
										response.BasicPremium += Math.Round(response.BasicPremium * System.Convert.ToDecimal(premVar.Rentalperc / 100), 2, MidpointRounding.AwayFromZero);
									}

									break;
							}
							break;
					}

					//Added on 23-Oct-2023 By Vincent
					// There will be a premium increase as per 1 Nov for NEW policies and 1-Jan for REN/APR Policies
					DSRateIncrease = Getdataset();
					if (DSRateIncrease != null)
					{
						rateIncrease = Convert.ToDecimal(DSRateIncrease.Tables[0].Rows[0]["Rate"].ToString());
						response.BasicPremium += response.BasicPremium * rateIncrease / 100;
					}

					//Added by Vincent 14  Jan 2008
					// Deal Correctly with Rateup
					if (request.RateUp > 0)
					{
						response.RateUpAmt = Math.Round(response.BasicPremium * System.Convert.ToDecimal(request.RateUp / 100), 2, MidpointRounding.AwayFromZero);
						response.RateUP = Convert.ToDecimal(request.RateUp);
						//response.BasicPremium = Math.Round(response.BasicPremium + response.RateUpAmt, 2, MidpointRounding.AwayFromZero);
						tmpBasicPremium = Math.Round(response.BasicPremium + response.RateUpAmt, 2, MidpointRounding.AwayFromZero);
					}
					else
					{
						tmpBasicPremium = Math.Round(response.BasicPremium, 2, MidpointRounding.AwayFromZero);
					}


					request.BasicPremium = response.BasicPremium;
				}
				response = CalcNoOverride(request, true);
			}
			catch (Exception exception)
			{
				MessageBox.Show("Vehicle with Numberplate: " + request.LicensePlateNo + " has an error");
				ErrorLogAdapter.Current.LogError(exception);
			}
			//'Set return
			return response;
		}

		private decimal CalcTPMONEUX(PremiumRequest request)
		{
			string where = "VehUse = '" + request.VehicleUse + "' and CountryCode = '" + request.Territory + "'";
			where = where + " and Age = '" + request.AgeCat + "' and EngineSize = '" + request.EngineSize + "'";
			List<ThirdPRateMONEUX> tpRateList = ThirdPRateMONEUXRepository.Current.Find(where, "ID");
			return Convert.ToDecimal(tpRateList[0].Premium);
		}

		#endregion "GetPremRateSEM"

		#region "GetPremRateDOM | Dominica"
		private PremiumResponse GetPremRateDOM(PremiumRequest request)
		{
		 req = request;
			PremVar premVar = null;
			CompPercentage compPercent = null;
			PremiumResponse response = new PremiumResponse();
			decimal vehvalue = request.VehicleValue;

			//decimal charge = 0;
			string coverage;
			decimal tmpBasicPremium = 0;
			//int year = 0;

			//int vehiclevalue = int.Parse(request.VehicleValue.ToString());

			int vehiclevalue = Convert.ToInt32(request.VehicleValue);


			try
			{
				coverage = request.Coverage;
				if (coverage == "TF") coverage = "C";
				List<Sys> sysList = SysRepository.Current.Find("CountryCode like '%" + request.Territory + "%'", "CountryCode");
				premVar = PremVarRepository.Current.GetByIDCoverageTerritory(request.VehicleUse, coverage, request.Territory)[0];

				if (request.NoOverride != true)
				{

					if (request.Coverage.Trim() == "C")
					{

						response.BasicPremium = (request.VehicleValue * request.CompPercDOM) / 100;
					}
					else if (request.Coverage == "T")
					{
						string tsql = "Territory ='" + request.Territory + "' and Coverage = '" + request.Coverage.Trim() + "' and VehUse = '" + request.VehicleUse + "' and VehType = '" + request.VehicleType + "'";
						compPercent = CompPercentageRepository.Current.Find(tsql, "VehUse")[0];

						response.BasicPremium = compPercent.Premium;
						if (request.AgeCat <= 30)
						{
							response.Deductible = compPercent.Deductibleunderage;
						}
						else
						{
							response.Deductible = compPercent.Deductible;
						}

					}
					else if (request.Coverage == "TF")
					{

					}

				}
				else
				{
					response.BasicPremium = request.BasicPremium;
				}
				if (request.NewVehDiscount)
				{
					response.BasicPremium = Math.Round(response.BasicPremium * sysList[0].Newvehdisc, 2, MidpointRounding.AwayFromZero);
				}
				//Added on 23-Oct-2023 By Vincent
				// There will be a premium increase as per 1 Nov for NEW policies and 1-Jan for REN/APR Policies
				DSRateIncrease = Getdataset();
				if (DSRateIncrease != null)
				{
					rateIncrease = Convert.ToDecimal(DSRateIncrease.Tables[0].Rows[0]["Rate"].ToString());
					response.BasicPremium += response.BasicPremium * rateIncrease / 100;
				}
				if (request.RateUp > 0)
				{
					response.RateUpAmt = Math.Round(response.BasicPremium * System.Convert.ToDecimal(request.RateUp / 100), 2, MidpointRounding.AwayFromZero);
					response.RateUP = Convert.ToDecimal(request.RateUp);
					//response.BasicPremium = Math.Round(response.BasicPremium + response.RateUpAmt, 2, MidpointRounding.AwayFromZero);
					tmpBasicPremium = Math.Round(response.BasicPremium + response.RateUpAmt, 2, MidpointRounding.AwayFromZero);
				}
				else
				{
					tmpBasicPremium = Math.Round(response.BasicPremium, 2, MidpointRounding.AwayFromZero);
				}
				if (request.Rental == false)
				{
					switch (request.VehicleUse)
					{
						case "PR":
							if (request.DriverExp < 2) request.AALD = true;
							break;
						case "BS":
						case "SB":
						case "CP":
						case "HD":
						case "TX":
						case "RN":
							request.AALD = true; //If vehicle use is commercial then AALD is automatically set to true
							break;
					}
					if (request.AALD && request.DriverExp > 2)
					{
						response.AALD = Math.Round(tmpBasicPremium * System.Convert.ToDecimal(premVar.Aald / 100), 2, MidpointRounding.AwayFromZero);
						tmpBasicPremium = Math.Round(tmpBasicPremium + response.AALD, 2, MidpointRounding.AwayFromZero);
					}
					else
					{
						if (request.DriverExp < 2) response.LicenseExp = Math.Round(tmpBasicPremium * System.Convert.ToDecimal(premVar.Licexp / 100), 2, MidpointRounding.AwayFromZero);
						tmpBasicPremium = Math.Round(tmpBasicPremium + response.LicenseExp, 2, MidpointRounding.AwayFromZero);
					}

				}

				response.AddTPL = request.AddTPL;
				response.ToolsOfTrade = request.ToolsOfTrade;
				tmpBasicPremium = Math.Round(tmpBasicPremium + response.AddTPL + response.ToolsOfTrade, 2, MidpointRounding.AwayFromZero);

				if (request.FleetDiscount)
				{
					response.Fleetdisc = Math.Round(tmpBasicPremium * System.Convert.ToDecimal(premVar.Fleetdisc / 100), 2, MidpointRounding.AwayFromZero);
					tmpBasicPremium = Math.Round(tmpBasicPremium - response.Fleetdisc, 2, MidpointRounding.AwayFromZero);
				}
				if (request.StaffDiscount)
				{
					response.StaffDisc = Math.Round(tmpBasicPremium * System.Convert.ToDecimal(premVar.Staffdisc / 100), 2, MidpointRounding.AwayFromZero);
					tmpBasicPremium = Math.Round(tmpBasicPremium - response.StaffDisc, 2, MidpointRounding.AwayFromZero);
				}

				if (request.Territory == "DOM" || request.Territory == "BVI")
				{

					if (request.ActOfGOD)
					{
						tmpBasicPremium = tmpBasicPremium + premVar.Actofgod;
						response.ActOfGOD = premVar.Actofgod;
					}

					if (request.Windscreen)
					{
						tmpBasicPremium = tmpBasicPremium + premVar.Windscreen;
						response.Windscreen = premVar.Windscreen;
					}

				}

				if (request.ManagDisc > 0)
				{
					response.ManagDisc = Math.Round(tmpBasicPremium * System.Convert.ToDecimal(request.ManagDisc / 100), 2, MidpointRounding.AwayFromZero);
					tmpBasicPremium = Math.Round(tmpBasicPremium - response.ManagDisc, 2, MidpointRounding.AwayFromZero);
				}

				response.YearPremium = tmpBasicPremium;
				response.GrossPremium = response.YearPremium;

				if (request.Period < 12 && request.ShortPeriod == false)
				{
					response.GrossPremium = Math.Round(response.YearPremium * System.Convert.ToDecimal(request.RateCode / 100), 2, MidpointRounding.AwayFromZero);
					switch (request.PremiumRateCode)
					{
						case "1Q":
							response.GrossPremium = Math.Round(response.YearPremium * System.Convert.ToDecimal(premVar.Quarterlyperc / 100), 2, MidpointRounding.AwayFromZero);
							break;
						case "3M50":
							response.GrossPremium = Math.Round(response.YearPremium / 2, 2, MidpointRounding.AwayFromZero);
							break;
						case "6M":
							response.GrossPremium = Math.Round(response.YearPremium * System.Convert.ToDecimal(premVar.Halfyearlyperc / 100), 2, MidpointRounding.AwayFromZero);
							break;
						case "9M50":
							response.GrossPremium = Math.Round(response.YearPremium / 2, 2, MidpointRounding.AwayFromZero);
							break;
					}
				}
				// add001-begin added by Vincent for short term calculation
				if (request.ShortPeriod)
				{
					response.GrossPremium = (response.YearPremium * request.ShortPeriod_Perc) / 100;
				}
				// add001-end end of added line

				if (request.NCD != 0) response.NCD = Math.Round(response.GrossPremium * System.Convert.ToDecimal(request.NCD / 100), 2, MidpointRounding.AwayFromZero);

				response.DiscountPremium = Math.Round(response.GrossPremium - response.NCD, 2, MidpointRounding.AwayFromZero);
				if (request.PassLiab > 0)
				{
					response.PassLiab = Math.Round(System.Convert.ToDecimal(request.PassLiab * premVar.Passliab), 2, MidpointRounding.AwayFromZero);
					if (request.ShortPeriod == false)
					{
						switch (request.PremiumRateCode)
						{
							case "1Q":
								response.PassLiab = Math.Round((response.PassLiab * System.Convert.ToDecimal(premVar.Quarterlyperc / 100)), 2, MidpointRounding.AwayFromZero);
								break;
							case "3M50":
								response.PassLiab = Math.Round(response.PassLiab / 2, 2, MidpointRounding.AwayFromZero);
								break;
							case "6M":
								response.PassLiab = Math.Round((response.PassLiab * System.Convert.ToDecimal(premVar.Halfyearlyperc / 100)), 2, MidpointRounding.AwayFromZero);
								break;
							case "9M50":
								response.PassLiab = Math.Round(response.PassLiab / 2, 2, MidpointRounding.AwayFromZero);
								break;
						}
					}
					else
					{
						response.PassLiab = Math.Round((response.PassLiab * System.Convert.ToDecimal(request.ShortPeriod_Perc / 100)), 2, MidpointRounding.AwayFromZero);
					}
				}
				if (request.isPercentageCampaign) response.CampaignAmt = ((response.DiscountPremium - response.NewVehDiscValue + response.PassLiab + request.ExtraCoverage1 + request.ExtraCoverage2) * request.Campaigns) / 100; else response.CampaignAmt = request.Campaigns;

				response.NetPremium = Math.Round(response.DiscountPremium + response.PassLiab + request.ExtraCoverage1 + request.ExtraCoverage2 + response.CampaignAmt, 2, MidpointRounding.AwayFromZero);
				response.PolicyFee = request.Policyfee;
				response.TotalPremium = Math.Round(response.NetPremium, 2, MidpointRounding.AwayFromZero) + response.PolicyFee + Math.Round(response.GovTax, 2, MidpointRounding.AwayFromZero);

				//New request, round to the nearest $0.05 as per 1 July 2015
				response.NetPremium = Math.Round((response.NetPremium / 0.05m), 0) * 0.05m;
				response.PolicyFee = Math.Round((response.PolicyFee / 0.05m), 0) * 0.05m;
				response.GovTax = Math.Round((response.GovTax / 0.05m), 0) * 0.05m;
				response.TotalPremium = Math.Round((response.TotalPremium / 0.05m), 0) * 0.05m;
			}

			catch (Exception exception)
			{
				ErrorLogAdapter.Current.LogError(exception);
			}
			return response;
		}
		#endregion "GetPremRateDOM"

		#region "GetPremRateSKT_NEV | St. Kitts & Nevis"
		private PremiumResponse GetPremRateSKT_NEV(PremiumRequest request)
		{
			CompRate premium = null;
			PremVar premVar = null;
			PremiumResponse response = new PremiumResponse();
			decimal vehvalue = request.VehicleValue;
			//decimal calcvalue = 0;
			decimal charge = 0;
			string coverage;
			decimal tmpBasicPremium = 0;

			try
			{
				coverage = request.Coverage;
				List<Sys> sysList = SysRepository.Current.Find("CountryCode like '%" + request.Territory + "%'", "CountryCode");
				premVar = PremVarRepository.Current.GetByIDCoverageTerritory(request.VehicleUse, coverage, request.Territory)[0];

				if (request.NoOverride != true)
				{

					//See if there is Extra Loads to be charged (e.g. Sports Car)
					List<Vtype> vTypes = VtypeRepository.Current.Find("Vtype = '" + request.VehicleType + "' and CountryCode like '%" + request.Territory + "%'", "CountryCode");
					string whereclause = "RateType = '" + request.VehicleUseText + "' AND CoverageType = '" + request.CoverageText + "' AND VehicleClass = '" + request.EngineSizeText + "'";
					List<VehicleRates> VehRates = VehicleRatesRepository.Current.Find(whereclause, "RateType");
					if (vTypes != null)
					{
						response.ExtraLoads = (System.Convert.ToDecimal(vTypes[0].Rateupperc) * VehRates[0].Rate);
					}
					tmpBasicPremium = Convert.ToDecimal(VehRates[0].Rate);
					response.BasicPremium = tmpBasicPremium;
					response.StartingPremium = tmpBasicPremium;
					if (request.AgeCat <= 25 || request.AgeCat > 26)
					{
						response.UnderAgePremium = (tmpBasicPremium * System.Convert.ToDecimal(premVar.Underageperc) / 100);
						response.BasicPremium = tmpBasicPremium + (tmpBasicPremium * System.Convert.ToDecimal(premVar.Underageperc) / 100);
					}
					if (request.DriverExp == 0) response.LicenseExp = Math.Round(tmpBasicPremium * System.Convert.ToDecimal(0.5), 2, MidpointRounding.AwayFromZero);
					if (request.DriverExp == 1) response.LicenseExp = Math.Round(tmpBasicPremium * System.Convert.ToDecimal(0.25), 2, MidpointRounding.AwayFromZero);
					tmpBasicPremium = Math.Round(response.BasicPremium + response.LicenseExp, 2, MidpointRounding.AwayFromZero);
					response.BasicPremium = tmpBasicPremium;

					#region "Comprehensive"
					if (request.Coverage == "C")
					{
						if (vehvalue > 10000)
						{
							request.VehicleValue = 10000;

							//This is to round the value of the vehicle to the next 1000
							int res = 0;
							int newVehValue = 0;

							System.Math.DivRem(Convert.ToInt32(vehvalue), 1000, out res);

							if (res > 0)
							{
								newVehValue = (Convert.ToInt32(vehvalue) - res) + 1000;
							}
							else
							{
								newVehValue = Convert.ToInt32(vehvalue);
							}

							charge = System.Math.Round((Convert.ToDecimal(newVehValue) - 10000) / 1000, 2, MidpointRounding.AwayFromZero) * premVar.Charge;
							response.ExtraPer1000Premium = System.Math.Round(charge, 2);
							response.BasicPremium += charge;
						}

					}

					#endregion "Comprehensive"
				}
				else
				{
					response.BasicPremium = request.BasicPremium;
				}
				if (request.NewVehDiscount)
				{
					response.BasicPremium = Math.Round(response.BasicPremium * sysList[0].Newvehdisc, 2, MidpointRounding.AwayFromZero);
				}
				response.RateUP = Math.Round(response.BasicPremium * System.Convert.ToDecimal(request.RateUp / 100), 2, MidpointRounding.AwayFromZero);
				response.RateUpAmt = response.RateUP;
				tmpBasicPremium = Math.Round(response.BasicPremium + response.RateUpAmt, 2, MidpointRounding.AwayFromZero);



				if (request.AALD)
				{
					if (request.Coverage == "C")
					{
						response.AALD = Convert.ToDecimal(premVar.Aald); //used to be a fixed 200;
					}
					else //request.Coverage == "T")
					{
						response.AALD = Math.Round(tmpBasicPremium * System.Convert.ToDecimal(premVar.Aald / 100), 2, MidpointRounding.AwayFromZero);
					}
					tmpBasicPremium = Math.Round(tmpBasicPremium + response.AALD, 2, MidpointRounding.AwayFromZero);
				}
				if (request.Coverage == "C")
				{
					if (request.Windscreen)
					{
						tmpBasicPremium = tmpBasicPremium + premVar.Windscreen;
						response.Windscreen = premVar.Windscreen;
					}
					if (request.ActOfGOD)
					{
						response.ActOfGOD = Math.Round((premVar.Actofgod * vehvalue), 2, MidpointRounding.AwayFromZero);
						tmpBasicPremium = tmpBasicPremium + response.ActOfGOD;
					}
				}
				else // No Windscreen or AOG coverage for TP
				{
					response.ActOfGOD = 0;
					response.Windscreen = 0;
				}

				response.AddTPL = request.AddTPL;
				response.ToolsOfTrade = request.ToolsOfTrade;
				tmpBasicPremium = Math.Round(tmpBasicPremium + response.AddTPL + response.ToolsOfTrade, 2, MidpointRounding.AwayFromZero);
				if (request.FleetDiscount)
				{
					response.Fleetdisc = Math.Round(tmpBasicPremium * System.Convert.ToDecimal(premVar.Fleetdisc / 100), 2, MidpointRounding.AwayFromZero);
					tmpBasicPremium = Math.Round(tmpBasicPremium - response.Fleetdisc, 2, MidpointRounding.AwayFromZero);
				}
				if (request.StaffDiscount)
				{
					response.StaffDisc = Math.Round(tmpBasicPremium * System.Convert.ToDecimal(premVar.Staffdisc / 100), 2, MidpointRounding.AwayFromZero);
					tmpBasicPremium = Math.Round(tmpBasicPremium - response.StaffDisc, 2, MidpointRounding.AwayFromZero);
				}



				tmpBasicPremium += response.ExtraLoads;
				if (request.ManagDisc > 0)
				{
					response.ManagDisc = Math.Round(tmpBasicPremium * System.Convert.ToDecimal(request.ManagDisc / 100), 2, MidpointRounding.AwayFromZero);
					tmpBasicPremium = Math.Round(tmpBasicPremium - response.ManagDisc, 2, MidpointRounding.AwayFromZero);
				}

				response.YearPremium = tmpBasicPremium;
				response.GrossPremium = response.YearPremium;

				if (request.Period < 12 && request.ShortPeriod == false)
				{
					response.GrossPremium = Math.Round(response.YearPremium * System.Convert.ToDecimal(request.RateCode / 100), 2, MidpointRounding.AwayFromZero);
					switch (request.PremiumRateCode)
					{
						case "1Q":
							response.GrossPremium = Math.Round(response.YearPremium * System.Convert.ToDecimal(premVar.Quarterlyperc / 100), 2, MidpointRounding.AwayFromZero);
							break;
						case "3M50":
							response.GrossPremium = Math.Round(response.YearPremium * System.Convert.ToDecimal(0.4), 2, MidpointRounding.AwayFromZero);
							break;
						case "9M50":
							response.GrossPremium = Math.Round(response.YearPremium * System.Convert.ToDecimal(0.6), 2, MidpointRounding.AwayFromZero);
							break;
						case "6M1":
							response.GrossPremium = Math.Round(response.YearPremium * System.Convert.ToDecimal(0.7), 2, MidpointRounding.AwayFromZero);
							break;
						case "6M2":
							response.GrossPremium = Math.Round(response.YearPremium * System.Convert.ToDecimal(0.3), 2, MidpointRounding.AwayFromZero);
							break;
					}
				}
				if (request.ShortPeriod)
				{
					response.GrossPremium = Math.Round(response.YearPremium * System.Convert.ToDecimal(request.ShortPeriod_Perc / 100), 2, MidpointRounding.AwayFromZero);
				}
				if (request.NCD != 0) response.NCD = Math.Round(response.GrossPremium * System.Convert.ToDecimal(request.NCD / 100), 2, MidpointRounding.AwayFromZero);

				response.DiscountPremium = Math.Round(response.GrossPremium - response.NCD, 2, MidpointRounding.AwayFromZero);
				if (request.PassLiab > 0)
				{
					response.PassLiab = Math.Round(System.Convert.ToDecimal(request.PassLiab * premVar.Passliab), 2, MidpointRounding.AwayFromZero);
					if (request.ShortPeriod == false)
					{
						switch (request.PremiumRateCode)
						{
							case "1Q":
								response.PassLiab = Math.Round((response.PassLiab * System.Convert.ToDecimal(premVar.Quarterlyperc / 100)), 2, MidpointRounding.AwayFromZero);
								break;
							case "3M50":
								response.PassLiab = Math.Round(response.PassLiab * System.Convert.ToDecimal(0.4), 2, MidpointRounding.AwayFromZero);
								break;
							case "9M50":
								response.PassLiab = Math.Round(response.PassLiab * System.Convert.ToDecimal(0.6), 2, MidpointRounding.AwayFromZero);
								break;
							case "6M1":
								response.PassLiab = Math.Round(response.PassLiab * System.Convert.ToDecimal(0.7), 2, MidpointRounding.AwayFromZero);
								break;
							case "6M2":
								response.PassLiab = Math.Round(response.PassLiab * System.Convert.ToDecimal(0.3), 2, MidpointRounding.AwayFromZero);
								break;
						}
					}
					else
					{
						response.PassLiab = Math.Round((response.PassLiab * System.Convert.ToDecimal(request.ShortPeriod_Perc / 100)), 2, MidpointRounding.AwayFromZero);
					}
				}
				if (request.isPercentageCampaign) response.CampaignAmt = ((response.DiscountPremium - response.NewVehDiscValue + response.PassLiab + request.ExtraCoverage1 + request.ExtraCoverage2) * request.Campaigns) / 100; else response.CampaignAmt = request.Campaigns;

				response.NetPremium = Math.Round(response.DiscountPremium + response.PassLiab + request.ExtraCoverage1 + request.ExtraCoverage2 + response.CampaignAmt, 2, MidpointRounding.AwayFromZero);
				response.PolicyFee = request.Policyfee;
				response.GovTax = (response.NetPremium + response.PolicyFee) * request.GovTax;

				//New request, round to the nearest $0.05 as per 1 July 2015
				response.NetPremium = Math.Round((response.NetPremium / 0.05m), 0) * 0.05m;
				response.PolicyFee = Math.Round((response.PolicyFee / 0.05m), 0) * 0.05m;
				response.GovTax = Math.Round((response.GovTax / 0.05m), 0) * 0.05m;
				response.TotalPremium = Math.Round(response.NetPremium, 2, MidpointRounding.AwayFromZero) + response.PolicyFee + Math.Round(response.GovTax, 2, MidpointRounding.AwayFromZero);
			}

			catch (Exception exception)
			{
				ErrorLogAdapter.Current.LogError(exception);
			}
			return response;
		}
		#endregion "GetPremRateSKT_NEV | St. Kitts & Nevis"

		#region "GetPremRateANG | Anguilla"
		private PremiumResponse GetPremRateANG(PremiumRequest request)
		{
			req = request;
			PremVar premVar = null;
			PremiumResponse response = new PremiumResponse();
			decimal vehvalue = request.VehicleValue;
			decimal tmpBasicPremium = 0;
			List<Sys> sysList = SysRepository.Current.Find("CountryCode like '%" + request.Territory + "%'", "CountryCode");
			premVar = PremVarRepository.Current.GetByIDCoverageTerritory(request.VehicleUse, request.Coverage, request.Territory)[0];
			if (request.NoOverride != true)
			{
				if (request.Coverage == "C")
				{
					string whereclause = "VehUse = '" + request.VehicleUse + "' AND Territory = '" + request.Territory + "'";
					List<CompPercentage> CompPerc = CompPercentageRepository.Current.Find(whereclause, "VehUse");
					if (CompPerc != null) tmpBasicPremium = Convert.ToDecimal(CompPerc[0].Percentage) * vehvalue;
				}
				if (request.Coverage == "T")
				{
					string whereclause = "ID = '" + request.VehicleUse + "' AND Territory = '" + request.Territory + "'";
					List<ThirdPRate> TPRate = ThirdPRateRepository.Current.Find(whereclause, "ID");
					if (TPRate != null) tmpBasicPremium = Convert.ToDecimal(TPRate[0].Over30under1600cc);
				}

				if (request.AgeCat <= 25 && request.InsuredSex == Gender.M)
				{
					tmpBasicPremium = tmpBasicPremium * 2;
				}
				if (request.AgeCat <= 25 && request.InsuredSex == Gender.F)
				{
					tmpBasicPremium = tmpBasicPremium * Convert.ToDecimal(1.5);
				}
				response.BasicPremium = tmpBasicPremium;

				//else
				//{
				//	response.BasicPremium = request.BasicPremium;
				//	tmpBasicPremium = request.BasicPremium;
				//}
				//Added on 23-Oct-2023 By Vincent
				// There will be a premium increase as per 1 Nov for NEW policies and 1-Jan for REN/APR Policies
				DSRateIncrease = Getdataset();
				if (DSRateIncrease != null)
				{


					rateIncrease = Convert.ToDecimal(DSRateIncrease.Tables[0].Rows[0]["Rate"].ToString());
					if (request.NCDDate == null) request.NCDDate = DateTime.Today;
					if (request.EffectiveDate == null) request.EffectiveDate = DateTime.Today;
					if (request.ApplicationDate == null) request.ApplicationDate = request.EffectiveDate;
					if (request.ApplicationDate > Convert.ToDateTime("1/1/2024") && request.IsNew == false)
					{
						response.BasicPremium = response.BasicPremium += response.BasicPremium * rateIncrease / 100;
					}

					else
					{
						if (request.NoOverride == false) response.BasicPremium += response.BasicPremium * rateIncrease / 100;
					}
					tmpBasicPremium = response.BasicPremium;
				}
			}
			else
			{
				response.BasicPremium = request.BasicPremium;
				tmpBasicPremium = request.BasicPremium;
			}
			// Other Calculations

			if (request.RateUp > 0)
			{
				response.RateUpAmt = Math.Round(response.BasicPremium * System.Convert.ToDecimal(request.RateUp / 100), 2, MidpointRounding.AwayFromZero);
				response.RateUP = Convert.ToDecimal(request.RateUp);
				tmpBasicPremium = Math.Round(response.BasicPremium + response.RateUpAmt, 2, MidpointRounding.AwayFromZero);
			}

			//tmpBasicPremium = response.BasicPremium;
			List<PremVar> premvarList = PremVarRepository.Current.Find("Territory = '" + request.Territory + "' and ID = '" + request.VehicleUse + "' and Coverage = '" + request.Coverage + "'", "Territory");
			if (request.AALD)
			{
				response.AALD = tmpBasicPremium * System.Convert.ToDecimal(premvarList[0].Aald / 100);
				tmpBasicPremium = Math.Round(tmpBasicPremium + response.AALD, 2, MidpointRounding.AwayFromZero);
			}
			if (request.ForLicense)
			{
				response.ForLicense = Math.Round(tmpBasicPremium * System.Convert.ToDecimal(premvarList[0].Forlicense / 100), 2, MidpointRounding.AwayFromZero);
				tmpBasicPremium = Math.Round(tmpBasicPremium + response.ForLicense, 2, MidpointRounding.AwayFromZero);
			}
			if (request.Windscreen)
			{
				tmpBasicPremium = tmpBasicPremium + premVar.Windscreen;
				response.Windscreen = premVar.Windscreen;
			}
			if (request.ActOfGOD)
			{
				response.ActOfGOD = (premVar.Actofgod * vehvalue);
				tmpBasicPremium = tmpBasicPremium + response.ActOfGOD;

			}
			response.AddTPL = request.AddTPL;
			response.ToolsOfTrade = request.ToolsOfTrade;
			tmpBasicPremium = Math.Round(tmpBasicPremium + response.AddTPL + response.ToolsOfTrade, 2, MidpointRounding.AwayFromZero);
			if (request.FleetDiscount)
			{
				response.Fleetdisc = Math.Round(tmpBasicPremium * System.Convert.ToDecimal(premVar.Fleetdisc / 100), 2, MidpointRounding.AwayFromZero);
				tmpBasicPremium = Math.Round(tmpBasicPremium - response.Fleetdisc, 2, MidpointRounding.AwayFromZero);
			}
			if (request.StaffDiscount)
			{
				response.StaffDisc = Math.Round(tmpBasicPremium * System.Convert.ToDecimal(premVar.Staffdisc / 100), 2, MidpointRounding.AwayFromZero);
				tmpBasicPremium = Math.Round(tmpBasicPremium - response.StaffDisc, 2, MidpointRounding.AwayFromZero);
			}



			tmpBasicPremium += response.ExtraLoads;
			if (request.ManagDisc > 0)
			{
				response.ManagDisc = Math.Round(tmpBasicPremium * System.Convert.ToDecimal(request.ManagDisc / 100), 2, MidpointRounding.AwayFromZero);
				tmpBasicPremium = Math.Round(tmpBasicPremium - response.ManagDisc, 2, MidpointRounding.AwayFromZero);
			}

			response.YearPremium = tmpBasicPremium;
			response.GrossPremium = response.YearPremium;

			if (request.Period < 12 && request.ShortPeriod == false)
			{
				response.GrossPremium = Math.Round(response.YearPremium * System.Convert.ToDecimal(request.RateCode / 100), 2, MidpointRounding.AwayFromZero);
				switch (request.PremiumRateCode)
				{
					case "1Q":
						response.GrossPremium = Math.Round(response.YearPremium * System.Convert.ToDecimal(premVar.Quarterlyperc / 100), 2, MidpointRounding.AwayFromZero);
						break;
					case "3M50":
						response.GrossPremium = Math.Round(response.YearPremium * System.Convert.ToDecimal(premVar.Threenineperc / 100), 2, MidpointRounding.AwayFromZero);
						break;
					case "9M50":
						response.GrossPremium = Math.Round(response.YearPremium * System.Convert.ToDecimal(premVar.Threenineperc / 100), 2, MidpointRounding.AwayFromZero);
						break;
					case "6M":
						response.GrossPremium = Math.Round(response.YearPremium * System.Convert.ToDecimal(premVar.Halfyearlyperc / 100), 2, MidpointRounding.AwayFromZero);
						break;

				}
			}
			if (request.ShortPeriod)
			{
				//response.GrossPremium = Math.Round(response.YearPremium * System.Convert.ToDecimal(request.ShortPeriod_Perc / 100), 2, MidpointRounding.AwayFromZero);
				decimal spGrosPrem = (response.YearPremium * System.Convert.ToDecimal(request.ShortPeriod_Perc)) / 100;
				response.GrossPremium = spGrosPrem;
			}
			if (request.NCD != 0) response.NCD = Math.Round(response.GrossPremium * System.Convert.ToDecimal(request.NCD / 100), 2, MidpointRounding.AwayFromZero);

			response.DiscountPremium = Math.Round(response.GrossPremium - response.NCD, 2, MidpointRounding.AwayFromZero);
			if (request.PassLiab > 0)
			{
				response.PassLiab = Math.Round(System.Convert.ToDecimal(request.PassLiab * premVar.Passliab), 2, MidpointRounding.AwayFromZero);
				if (request.ShortPeriod == false)
				{
					switch (request.PremiumRateCode)
					{
						case "1Q":
							response.PassLiab = Math.Round((response.PassLiab * System.Convert.ToDecimal(premVar.Quarterlyperc / 100)), 2, MidpointRounding.AwayFromZero);
							break;
						case "3M50":
							response.PassLiab = Math.Round(response.PassLiab * System.Convert.ToDecimal(premVar.Threenineperc / 100), 2, MidpointRounding.AwayFromZero);
							break;
						case "9M50":
							response.PassLiab = Math.Round(response.PassLiab * System.Convert.ToDecimal(premVar.Threenineperc / 100), 2, MidpointRounding.AwayFromZero);
							break;
						case "6M":
							response.PassLiab = Math.Round(response.PassLiab * System.Convert.ToDecimal(premVar.Halfyearlyperc / 100), 2, MidpointRounding.AwayFromZero);
							break;
					}
				}
				else
				{
					response.PassLiab = Math.Round((response.PassLiab * System.Convert.ToDecimal(request.ShortPeriod_Perc / 100)), 2, MidpointRounding.AwayFromZero);
				}
			}
			if (request.isPercentageCampaign) response.CampaignAmt = ((response.DiscountPremium - response.NewVehDiscValue + response.PassLiab + request.ExtraCoverage1 + request.ExtraCoverage2) * request.Campaigns) / 100; else response.CampaignAmt = request.Campaigns;
			response.NetPremium = Math.Round(response.DiscountPremium + response.PassLiab + request.ExtraCoverage1 + request.ExtraCoverage2 + response.CampaignAmt, 2, MidpointRounding.AwayFromZero);
			response.PolicyFee = request.Policyfee;
			response.TotalPremium = Math.Round(response.NetPremium, 2, MidpointRounding.AwayFromZero) + response.PolicyFee + Math.Round(response.GovTax, 2, MidpointRounding.AwayFromZero);

			//see if the NetPremium is more than the minimal premium
			List<MinimumPremium> minprem = MinimumPremiumController.Find("PolTyp= '" + request.PolType + "' and string1 = '" + request.Coverage + "' and string2 like '%" + request.VehicleUse + "%'", "PolTyp");
			if (request.overrideMinPremium == false)
			{
				if (minprem != null || minprem.Count > 0)
				{
					try
					{
						switch (request.PremiumRateCode)
						{
							case "1Q":
								if ((response.NetPremium) < minprem[0].Premium / 4)
								{
									response.NetPremium = minprem[0].Premium / 4;
									response.Message = "Minimum premium was applied";
								}
								break;
							case "3M50":
							case "9M50":
							case "6M":
								if ((response.NetPremium) < minprem[0].Premium / 2)
								{
									response.NetPremium = minprem[0].Premium / 2;
									response.Message = "Minimum premium was applied";
								}
								break;
							case "1Y":
								if ((response.NetPremium) < minprem[0].Premium)
								{
									response.NetPremium = minprem[0].Premium;
									response.Message = "Minimum premium was applied";
								}
								break;
						}
						response.GovTax = response.NetPremium * request.GovTax;
						response.TotalPremium = Math.Round(response.NetPremium, 2, MidpointRounding.AwayFromZero) + response.PolicyFee + Math.Round(response.GovTax, 2, MidpointRounding.AwayFromZero);

					}
					catch (Exception exception)
					{
						ErrorLogAdapter.Current.LogError(exception);
					}

				}
			}
			//New request, round to the nearest $0.05 as per 1 July 2015
			response.NetPremium = Math.Round((response.NetPremium / 0.05m), 0) * 0.05m;
			response.PolicyFee = Math.Round((response.PolicyFee / 0.05m), 0) * 0.05m;
			response.GovTax = Math.Round((response.GovTax / 0.05m), 0) * 0.05m;
			response.GovVAT = Math.Round((response.GovVAT / 0.05m), 0) * 0.05m;
			response.TotalPremium = Math.Round((response.TotalPremium / 0.05m), 0) * 0.05m;
			return response;
		}
		#endregion "GetPremRateANG"

		#region "GetPremRateBVI | Tortola"
		private PremiumResponse GetPremRateBVI(PremiumRequest request)
		{
			req = request;
			CompRate premium = null;
			PremVar premVar = null;
			ThirdPRate tppremium = null;
			PremiumResponse response = new PremiumResponse();
			VehicleRateCriteria vehratecriteria = null;
			decimal vehvalue = request.VehicleValue;
			decimal charge = 0;
			string coverage;
			decimal tmpBasicPremium = 0;

			//int vehiclevalue = int.Parse(request.VehicleValue.ToString());

			int vehiclevalue = Convert.ToInt32(request.VehicleValue);


			try
			{
				coverage = request.Coverage;
				if (coverage == "TF") coverage = "C";
				List<Sys> sysList = SysRepository.Current.Find("CountryCode like '%" + request.Territory + "%'", "CountryCode");
				premVar = PremVarRepository.Current.GetByIDCoverageTerritory(request.VehicleUse, coverage, request.Territory)[0];

				if (request.NoOverride != true)
				{

					#region "Comprehensive / TF"
					if (request.Coverage == "C" || request.Coverage == "TF")
					{
						//if (request.Rental == false)
						//{
						if (vehvalue > 10000)
						{
							request.VehicleValue = 10000;

							//This is to round the value of the vehicle to the next 1000
							int res = 0;
							int newVehValue = 0;

							System.Math.DivRem(Convert.ToInt32(vehvalue), 1000, out res);

							if (res > 0)
							{
								newVehValue = (Convert.ToInt32(vehvalue) - res) + 1000;
							}
							else
							{
								newVehValue = Convert.ToInt32(vehvalue);
							}

							charge = System.Math.Round((Convert.ToDecimal(newVehValue) - 10000) / 1000, MidpointRounding.AwayFromZero) * premVar.Charge;

						}
						premium = CompRateRepository.Current.GetCompRate(Convert.ToInt32(request.VehicleValue), request.CurrCode, request.VehicleUse, request.Territory)[0];

						response.BasicPremium = premium.Over30;

						response.BasicPremium += charge;

						if (request.Coverage == "TF")
						{
							response.BasicPremium = Math.Round(response.BasicPremium * System.Convert.ToDecimal(75 / 100), 2, MidpointRounding.AwayFromZero);
						}
						premium = CompRateRepository.Current.GetCompRate(Convert.ToInt32(vehvalue), request.CurrCode, request.VehicleUse, request.Territory)[0];
						response.Deductible = Convert.ToDecimal(premium.Sumins); //Used as deductible

						//}
						//else
						//{
						//    response.BasicPremium = Math.Round(vehiclevalue * System.Convert.ToDecimal(premVar.Rentalperc / 100), 2, MidpointRounding.AwayFromZero);
						//}
					}
					#endregion "Comprehensive / TF"

					#region "Third Party"
					else if (request.Coverage == "T")
					{

						string tsql = "ID ='" + request.VehicleUse + "'";
						//List<ThirdPRate> thirdpartyrate = ThirdPRateRepository.Current.Find("ID ='" + request.VehicleUse + "%'", "ID");

						tppremium = ThirdPRateRepository.Current.Find(tsql, "ID")[0];

						response.BasicPremium = tppremium.Over30under1600cc; // Premium
						response.Deductible = tppremium.Under30under1600cc; // Deductible
					}
					#endregion "Third Party"
				}
				else
				{
					response.BasicPremium = request.BasicPremium;
				}

				//Added on 23-Oct-2023 By Vincent
				// There will be a premium increase as per 1 Nov for NEW policies and 1-Jan for REN/APR Policies
				DSRateIncrease = Getdataset();
				if (DSRateIncrease != null)
				{
					rateIncrease = Convert.ToDecimal(DSRateIncrease.Tables[0].Rows[0]["Rate"].ToString());
					response.BasicPremium += response.BasicPremium * rateIncrease / 100;
				}

				if (request.RateUp > 0)
				{
					response.RateUpAmt = Math.Round(response.BasicPremium * System.Convert.ToDecimal(request.RateUp / 100), 2, MidpointRounding.AwayFromZero);
					response.RateUP = Convert.ToDecimal(request.RateUp);
					//response.BasicPremium = Math.Round(response.BasicPremium + response.RateUpAmt, 2, MidpointRounding.AwayFromZero);
					tmpBasicPremium = Math.Round(response.BasicPremium + response.RateUpAmt, 2, MidpointRounding.AwayFromZero);
				}
				else
				{
					tmpBasicPremium = Math.Round(response.BasicPremium, 2, MidpointRounding.AwayFromZero);
				}

				//
				//calculate underage premium and deductible
				string tsql2 = "VehRate ='" + request.VehicleUse + "'";
				vehratecriteria = VehicleRateCriteriaRepository.Current.Find(tsql2, "VehRate")[0];
				//
				switch (request.AgeCat.ToString())
				{
					case "21": //Under 22
						if (request.InsuredSex == Gender.M)
						{
							tmpBasicPremium = Math.Round(tmpBasicPremium + (tmpBasicPremium * vehratecriteria.MaleUnder22IncrPerc), 2, MidpointRounding.AwayFromZero);
							response.Deductible = vehratecriteria.MaleUnder22DeductAmt;
						}
						if (request.InsuredSex == Gender.F)
						{
							tmpBasicPremium = Math.Round(tmpBasicPremium + (tmpBasicPremium * vehratecriteria.FemaleUnder22IncrPerc / 100), 2, MidpointRounding.AwayFromZero);
							response.Deductible = vehratecriteria.FemaleUnder22DeductAmt;
						}

						break;
					case "24": //Under 25
						if (request.InsuredSex == Gender.M)
						{
							tmpBasicPremium = Math.Round(tmpBasicPremium + (tmpBasicPremium * vehratecriteria.MaleUnder22IncrPerc / 100), 2, MidpointRounding.AwayFromZero);
							response.Deductible = vehratecriteria.MaleUnder25DeductAmt;
						}
						if (request.InsuredSex == Gender.F)
						{
							tmpBasicPremium = Math.Round(tmpBasicPremium + (tmpBasicPremium * vehratecriteria.FemaleUnder22IncrPerc / 100), 2, MidpointRounding.AwayFromZero);
							response.Deductible = vehratecriteria.FemaleUnder22DeductAmt;
						}
						break;
					case "26":
						break;

				}

				if (request.Rental == false)
				{
					//switch (request.VehicleUse)
					//{
					//    case "PR":
					//        if (request.DriverExp < 2) request.AALD = true;
					//        break;
					//    case "BS":
					//    case "SB":
					//    case "CP":
					//    case "HD":
					//    case "TX":
					//    case "RN":
					//        request.AALD = true; //If vehicle use is commercial then AALD is automatically set to true
					//        break;
					//}
					if (request.AALD && request.DriverExp > 2)
					{
						response.AALD = Math.Round(tmpBasicPremium * System.Convert.ToDecimal(premVar.Aald / 100), 2, MidpointRounding.AwayFromZero);
						tmpBasicPremium = Math.Round(tmpBasicPremium + response.AALD, 2, MidpointRounding.AwayFromZero);
					}
					else
					{
						if (request.DriverExp < 2) response.LicenseExp = Math.Round(tmpBasicPremium * System.Convert.ToDecimal(premVar.Licexp / 100), 2, MidpointRounding.AwayFromZero);
						tmpBasicPremium = Math.Round(tmpBasicPremium + response.LicenseExp, 2, MidpointRounding.AwayFromZero);
					}

				}

				//Removed from this section as per Shan's request. It should not be susceptible to NCD

				//if (request.ActOfGOD == true)
				//{
				//    response.ActOfGOD = (premVar.Actofgod * vehvalue) / 100;
				//    tmpBasicPremium = tmpBasicPremium + response.ActOfGOD;
				//}

				//if (request.Windscreen == true)
				//{
				//    tmpBasicPremium = tmpBasicPremium + premVar.Windscreen;
				//    response.Windscreen = premVar.Windscreen;
				//}

				response.AddTPL = request.AddTPL;
				tmpBasicPremium = Math.Round(tmpBasicPremium + response.AddTPL, 2, MidpointRounding.AwayFromZero);

				if (request.FleetDiscount)
				{
					response.Fleetdisc = Math.Round(tmpBasicPremium * System.Convert.ToDecimal(premVar.Fleetdisc / 100), 2, MidpointRounding.AwayFromZero);
					tmpBasicPremium = Math.Round(tmpBasicPremium - response.Fleetdisc, 2, MidpointRounding.AwayFromZero);
				}
				if (request.StaffDiscount)
				{
					response.StaffDisc = Math.Round(tmpBasicPremium * System.Convert.ToDecimal(premVar.Staffdisc / 100), 2, MidpointRounding.AwayFromZero);
					tmpBasicPremium = Math.Round(tmpBasicPremium - response.StaffDisc, 2, MidpointRounding.AwayFromZero);
				}




				if (request.ManagDisc > 0)
				{
					response.ManagDisc = Math.Round(tmpBasicPremium * System.Convert.ToDecimal(request.ManagDisc / 100), 2, MidpointRounding.AwayFromZero);
					tmpBasicPremium = Math.Round(tmpBasicPremium - response.ManagDisc, 0, MidpointRounding.AwayFromZero);
				}

				//Tools of Trade Moved here after request on July 27, 2017
				response.ToolsOfTrade = request.ToolsOfTrade;
				tmpBasicPremium = Math.Round(tmpBasicPremium + response.ToolsOfTrade, 2, MidpointRounding.AwayFromZero);


				response.YearPremium = Math.Round(tmpBasicPremium, 0, MidpointRounding.AwayFromZero);
				response.GrossPremium = response.YearPremium;

				if (request.Period < 12 && request.ShortPeriod == false)
				{
					response.GrossPremium = Math.Round(response.YearPremium * System.Convert.ToDecimal(request.RateCode / 100), 0, MidpointRounding.AwayFromZero);
					switch (request.PremiumRateCode)
					{
						case "1Q":
							response.GrossPremium = Math.Round(response.YearPremium * System.Convert.ToDecimal(premVar.Quarterlyperc / 100), 0, MidpointRounding.AwayFromZero);
							break;
						case "6M":
							response.GrossPremium = Math.Round(response.YearPremium * System.Convert.ToDecimal(premVar.Halfyearlyperc / 100), 0, MidpointRounding.AwayFromZero);
							break;
					}
				}
				// add001-begin added by Vincent for short term calculation
				if (request.ShortPeriod)
				{
					response.GrossPremium = (response.YearPremium * request.ShortPeriod_Perc) / 100;
				}
				// add001-end end of added line

				if (request.NCD != 0) response.NCD = Math.Round(response.GrossPremium * System.Convert.ToDecimal(request.NCD / 100), 0, MidpointRounding.AwayFromZero);

				response.DiscountPremium = Math.Round(response.GrossPremium - response.NCD, 0, MidpointRounding.AwayFromZero);




				//Moved here to calculate after NCD

				if (request.ActOfGOD)
				{
					response.ActOfGOD = Math.Round(((premVar.Actofgod * vehvalue) / 100), 0, MidpointRounding.AwayFromZero);
					switch (request.PremiumRateCode)
					{
						case "1Q":
							response.ActOfGOD = Math.Round(response.ActOfGOD * System.Convert.ToDecimal(premVar.Quarterlyperc / 100), 0, MidpointRounding.AwayFromZero);
							break;
						case "6M":
							response.ActOfGOD = Math.Round(response.ActOfGOD * System.Convert.ToDecimal(premVar.Halfyearlyperc / 100), 0, MidpointRounding.AwayFromZero);
							break;
					}
					response.DiscountPremium = response.DiscountPremium + response.ActOfGOD;
				}

				if (request.Windscreen)
				{
					response.Windscreen = premVar.Windscreen;
					switch (request.PremiumRateCode)
					{
						case "1Q":
							response.Windscreen = Math.Round(response.Windscreen * System.Convert.ToDecimal(premVar.Quarterlyperc / 100), 0, MidpointRounding.AwayFromZero);
							break;
						case "6M":
							response.Windscreen = Math.Round(response.Windscreen * System.Convert.ToDecimal(premVar.Halfyearlyperc / 100), 0, MidpointRounding.AwayFromZero);
							break;
					}
					response.DiscountPremium = response.DiscountPremium + response.Windscreen;
				}
				//-----------------------------------------------------------

				if (request.PassLiab > 0)
				{
					response.PassLiab = Math.Round(System.Convert.ToDecimal(request.PassLiab * premVar.Passliab), 2, MidpointRounding.AwayFromZero);
					if (request.ShortPeriod == false)
					{
						switch (request.PremiumRateCode)
						{
							case "1Q":
								response.PassLiab = Math.Round((response.PassLiab * System.Convert.ToDecimal(premVar.Quarterlyperc / 100)), 0, MidpointRounding.AwayFromZero);
								break;
							case "6M":
								response.PassLiab = Math.Round((response.PassLiab * System.Convert.ToDecimal(premVar.Halfyearlyperc / 100)), 0, MidpointRounding.AwayFromZero);
								break;
						}
					}
					else
					{
						response.PassLiab = Math.Round((response.PassLiab * System.Convert.ToDecimal(request.ShortPeriod_Perc / 100)), 0, MidpointRounding.AwayFromZero);
					}
				}

				if (request.isPercentageCampaign) response.CampaignAmt = ((response.DiscountPremium - response.NewVehDiscValue + response.PassLiab + request.ExtraCoverage1 + request.ExtraCoverage2) * request.Campaigns) / 100; else response.CampaignAmt = request.Campaigns;
				response.NRSA = request.NRSA;
				response.NetPremium = Math.Round(response.DiscountPremium + response.PassLiab + request.ExtraCoverage1 + request.ExtraCoverage2 + response.CampaignAmt, 0, MidpointRounding.AwayFromZero);
				if (response.CampaignAmt == response.NRSA) response.NetPremium = Math.Round(response.DiscountPremium + response.PassLiab + request.ExtraCoverage1 + request.ExtraCoverage2, 0, MidpointRounding.AwayFromZero);
				response.PolicyFee = request.Policyfee;

				response.TotalPremium = Math.Round(response.NetPremium, 0, MidpointRounding.AwayFromZero) + response.PolicyFee + response.NRSA + Math.Round(response.GovTax, 0, MidpointRounding.AwayFromZero);
				// if (response.CampaignAmt == response.NRSA) response.TotalPremium = Math.Round((response.TotalPremium - response.NRSA), 0, MidpointRounding.AwayFromZero);
			}

			catch (Exception exception)
			{
				ErrorLogAdapter.Current.LogError(exception);
			}
			return response;
		}
		#endregion "GetPremRateBVI"

		#region "GetPremRateSLU | St. Lucia"
		private PremiumResponse GetPremRateSLU(PremiumRequest request)
		{
			req = request;
			CompRate premium = null;
			PremVar premVar = null;
			PremiumResponse response = new PremiumResponse();
			decimal vehvalue = request.VehicleValue;
			decimal calcvalue = 0;
			decimal charge = 0;
			string coverage;
			decimal tmpBasicPremium = 0;


			int vehiclevalue = Convert.ToInt32(request.VehicleValue);
			if (request.Territory == "BAH" || request.Territory == "TCI")
			{
				response = CalcNoOverride(request, true);
				return response;
			}

			try
			{
				coverage = request.Coverage;
				//if (coverage == "C") ;
				List<Sys> sysList = SysRepository.Current.Find("CountryCode like '%" + request.Territory + "%'", "CountryCode");
				premVar = PremVarRepository.Current.GetByIDCoverageTerritory(request.VehicleUse, coverage, request.Territory)[0];
				int YearSLU = (NAGICO.CalculateAge(request.VehicleYear) - 1);
				if (YearSLU < 0)
				{
					YearSLU = YearSLU * -1;
				}
				if (request.NoOverride != true)
				{

					#region "Comprehensive"
					if (request.Coverage == "C")
					{
						if (request.Rental == false)
						{
							//BindingList<ComprehensiveRateStLucia> b = ComprehensiveRateStLuciaAdapter.Current.Find(p => p.Year == YearSLU && p.EngineSize == request.EngineSize && p.VehUse == request.VehicleUse);
							BindingList<ComprehensiveRateStLucia> b = ComprehensiveRateStLuciaAdapter.Current.Find(p => p.VehUse == request.VehicleUse);
							if (b == null)
							{
								return response;
							}
							tmpBasicPremium = Convert.ToDecimal(b[0].PlusCat) * request.VehicleValue / 100;

						}
						else
						{
							tmpBasicPremium = Math.Round(vehiclevalue * System.Convert.ToDecimal(premVar.Rentalperc / 100), 2, MidpointRounding.AwayFromZero);
						}
					}
					#endregion "Comprehensive"

					if (request.Coverage == "TF")   // Basic Comprehensive
					{
						//if (request.Rental == false)
						//{
						//    BindingList<ComprehensiveRateStLucia> b = ComprehensiveRateStLuciaAdapter.Current.Find(p => p.VehUse == request.VehicleUse);
						//    if (b == null)
						//    {
						//        return response;
						//    }
						//    tmpBasicPremium = Convert.ToDecimal(b[0].RevBase) * request.VehicleValue / 100;

						//}
						//else
						//{
						//    tmpBasicPremium = Math.Round(vehiclevalue * System.Convert.ToDecimal(premVar.Rentalperc / 100), 2, MidpointRounding.AwayFromZero);
						//}

						string Engine = request.EngineSize.ToString();
						BindingList<ThirdPartyRates> tp = ThirdPartyRatesAdapter.Current.Find(p => p.RatesCategorie == request.VehicleUse && p.ObjectCategorie == Engine);
						if (tp.Count == 0)
						{
							return response;
						}
						tmpBasicPremium = Convert.ToDecimal(tp[0].Age) * request.VehicleValue / 100;
					}

					if (request.Coverage == "T")
					{
						string Engine = request.EngineSize.ToString();
						BindingList<ThirdPartyRates> tp = ThirdPartyRatesAdapter.Current.Find(p => p.RatesCategorie == request.VehicleUse && p.ObjectCategorie == Engine);
						if (tp.Count == 0)
						{
							return response;
						}
						tmpBasicPremium = Convert.ToDecimal(tp[0].Amount);

					}

					//Added on 23-Oct-2023 By Vincent
					// There will be a premium increase as per 1 Nov for NEW policies and 1-Jan for REN/APR Policies
					DSRateIncrease = Getdataset();
					if (DSRateIncrease != null)
					{
						rateIncrease = Convert.ToDecimal(DSRateIncrease.Tables[0].Rows[0]["Rate"].ToString());
						response.BasicPremium += response.BasicPremium * rateIncrease / 100;
					}

					//See if there is inexperience driver rates to be charged (e.g. underage male)
					if (request.DriverExp == 0 & request.AgeCat <= 25) response.LicenseExp = Math.Round(tmpBasicPremium * System.Convert.ToDecimal(0.5), 2, MidpointRounding.AwayFromZero);
					else if (request.DriverExp == 1 && request.AgeCat <= 25) response.LicenseExp = Math.Round(tmpBasicPremium * System.Convert.ToDecimal(0.35), 2, MidpointRounding.AwayFromZero);
					//See if there is inexperience driver over 25
					else if (request.DriverExp == 0) response.LicenseExp = Math.Round(tmpBasicPremium * System.Convert.ToDecimal(0.35), 2, MidpointRounding.AwayFromZero);
					tmpBasicPremium = Math.Round(tmpBasicPremium + response.LicenseExp, 2, MidpointRounding.AwayFromZero);
					response.BasicPremium = tmpBasicPremium;

					////See if there is inexperience driver over 25
					//if (request.DriverExp == 0)
					//{
					//    response.BasicPremium = tmpBasicPremium + (Math.Round(tmpBasicPremium * System.Convert.ToDecimal(0.35), 2, MidpointRounding.AwayFromZero);
					//}				   		   
				}
				else
				{
					response.BasicPremium = request.BasicPremium;
				}
				if (request.NewVehDiscount)
				{
					response.BasicPremium = Math.Round(response.BasicPremium * sysList[0].Newvehdisc, 2, MidpointRounding.AwayFromZero);
				}
				if (request.RateUp > 0)
				{
					response.RateUpAmt = Math.Round(response.BasicPremium * System.Convert.ToDecimal(request.RateUp / 100), 2, MidpointRounding.AwayFromZero);
					response.RateUP = Convert.ToDecimal(request.RateUp);
					//response.BasicPremium = Math.Round(response.BasicPremium + response.RateUpAmt, 2, MidpointRounding.AwayFromZero);
					tmpBasicPremium = Math.Round(response.BasicPremium + response.RateUpAmt, 2, MidpointRounding.AwayFromZero);
				}
				else
				{
					tmpBasicPremium = Math.Round(response.BasicPremium, 2, MidpointRounding.AwayFromZero);
				}


				if (request.Rental == false)
				{
					if (request.AALD)
					{

						response.AALD = Math.Round(tmpBasicPremium * System.Convert.ToDecimal(premVar.Aald / 100), 2, MidpointRounding.AwayFromZero);
						tmpBasicPremium = Math.Round(tmpBasicPremium + response.AALD, 2, MidpointRounding.AwayFromZero);
					}
				}
				if (request.Windscreen)
				{
					tmpBasicPremium = tmpBasicPremium + premVar.Windscreen;
					response.Windscreen = premVar.Windscreen;
				}
				if (request.ActOfGOD && request.Coverage == "C")
				{
					response.ActOfGOD = (premVar.Actofgod * vehvalue);
					tmpBasicPremium = tmpBasicPremium + response.ActOfGOD;
				}
				//Added so that SLu can manually add winscreen extra charge
				if (request.CompPercDOM > 0 && request.Coverage == "C")
				{
					tmpBasicPremium = tmpBasicPremium + request.CompPercDOM;
					response.ForLicense = request.CompPercDOM;
				}

				response.AddTPL = request.AddTPL;
				response.ToolsOfTrade = request.ToolsOfTrade;
				tmpBasicPremium = Math.Round(tmpBasicPremium + response.AddTPL + response.ToolsOfTrade, 2, MidpointRounding.AwayFromZero);
				if (request.FleetDiscount)
				{
					response.Fleetdisc = Math.Round(tmpBasicPremium * System.Convert.ToDecimal(premVar.Fleetdisc / 100), 2, MidpointRounding.AwayFromZero);
					tmpBasicPremium = Math.Round(tmpBasicPremium - response.Fleetdisc, 2, MidpointRounding.AwayFromZero);
				}
				if (request.StaffDiscount)
				{
					response.StaffDisc = Math.Round(tmpBasicPremium * System.Convert.ToDecimal(premVar.Staffdisc / 100), 2, MidpointRounding.AwayFromZero);
					tmpBasicPremium = Math.Round(tmpBasicPremium - response.StaffDisc, 2, MidpointRounding.AwayFromZero);
				}



				tmpBasicPremium += response.ExtraLoads;
				if (request.ManagDisc > 0)
				{
					response.ManagDisc = Math.Round(tmpBasicPremium * System.Convert.ToDecimal(request.ManagDisc / 100), 2, MidpointRounding.AwayFromZero);
					tmpBasicPremium = Math.Round(tmpBasicPremium - response.ManagDisc, 2, MidpointRounding.AwayFromZero);
				}

				response.YearPremium = tmpBasicPremium;
				response.GrossPremium = response.YearPremium;

				if (request.Period < 12 && request.ShortPeriod == false)
				{
					response.GrossPremium = Math.Round(response.YearPremium * System.Convert.ToDecimal(request.RateCode / 100), 2, MidpointRounding.AwayFromZero);
					switch (request.PremiumRateCode)
					{
						case "1Q":
							response.GrossPremium = Math.Round(response.YearPremium * System.Convert.ToDecimal(premVar.Quarterlyperc / 100), 2, MidpointRounding.AwayFromZero);
							break;
						case "3M":
							response.GrossPremium = Math.Round(response.YearPremium * System.Convert.ToDecimal(0.4), 2, MidpointRounding.AwayFromZero);
							break;
						case "9M":
							response.GrossPremium = Math.Round(response.YearPremium * System.Convert.ToDecimal(0.6), 2, MidpointRounding.AwayFromZero);
							break;
						case "6M1":
							response.GrossPremium = Math.Round(response.YearPremium * System.Convert.ToDecimal(0.7), 2, MidpointRounding.AwayFromZero);
							break;
						case "6M2":
							response.GrossPremium = Math.Round(response.YearPremium * System.Convert.ToDecimal(0.3), 2, MidpointRounding.AwayFromZero);
							break;

						case "SPSLU":
							//BindingList<ShortPeriodDetail> sps = ShortPeriodDetailAdapter.Current.Find(NAGICO.NumberOfDays(request.EffectiveDate, request.RenewalDate));
							//response.GrossPremium = Math.Round(response.YearPremium * sps[0].Percentage / 100, 2, MidpointRounding.AwayFromZero);
							break;
					}
				}
				if (request.ShortPeriod)
				{
					response.GrossPremium = (response.YearPremium * request.ShortPeriod_Perc) / 100;
				}
				if (request.NCD != 0) response.NCD = Math.Round(response.GrossPremium * System.Convert.ToDecimal(request.NCD / 100), 2, MidpointRounding.AwayFromZero);

				response.DiscountPremium = Math.Round(response.GrossPremium - response.NCD, 2, MidpointRounding.AwayFromZero);
				if (request.PassLiab > 0)
				{
					response.PassLiab = Math.Round(System.Convert.ToDecimal(request.PassLiab * premVar.Passliab), 2, MidpointRounding.AwayFromZero);
					if (request.ShortPeriod == false)
					{
						switch (request.PremiumRateCode)
						{
							case "1Q":
								response.PassLiab = Math.Round((response.PassLiab * System.Convert.ToDecimal(premVar.Quarterlyperc / 100)), 2, MidpointRounding.AwayFromZero);
								break;
							case "3M":
								response.PassLiab = Math.Round(response.PassLiab * System.Convert.ToDecimal(0.4), 2, MidpointRounding.AwayFromZero);
								break;
							case "9M":
								response.PassLiab = Math.Round(response.PassLiab * System.Convert.ToDecimal(0.4), 2, MidpointRounding.AwayFromZero);
								break;
							case "6M1":
								response.PassLiab = Math.Round(response.PassLiab * System.Convert.ToDecimal(0.7), 2, MidpointRounding.AwayFromZero);
								break;
							case "6M2":
								response.PassLiab = Math.Round(response.PassLiab * System.Convert.ToDecimal(0.3), 2, MidpointRounding.AwayFromZero);
								break;
						}
					}
					else
					{
						response.PassLiab = Math.Round((response.PassLiab * System.Convert.ToDecimal(request.ShortPeriod_Perc / 100)), 2, MidpointRounding.AwayFromZero);
					}
				}
				if (request.isPercentageCampaign) response.CampaignAmt = ((response.DiscountPremium - response.NewVehDiscValue + response.PassLiab + request.ExtraCoverage1 + request.ExtraCoverage2) * request.Campaigns) / 100; else response.CampaignAmt = request.Campaigns;
				response.NetPremium = Math.Round(response.DiscountPremium + response.PassLiab + request.ExtraCoverage1 + request.ExtraCoverage2 + response.CampaignAmt, 2, MidpointRounding.AwayFromZero);
				response.PolicyFee = request.Policyfee;
				response.TotalPremium = Math.Round(response.NetPremium, 2, MidpointRounding.AwayFromZero) + response.PolicyFee + Math.Round(response.GovTax, 2, MidpointRounding.AwayFromZero);
				//New request, round to the nearest $0.05 as per 1 July 2015
				response.NetPremium = Math.Round((response.NetPremium / 0.05m), 0) * 0.05m;
				response.PolicyFee = Math.Round((response.PolicyFee / 0.05m), 0) * 0.05m;
				response.GovTax = Math.Round((response.GovTax / 0.05m), 0) * 0.05m;
				response.TotalPremium = Math.Round((response.TotalPremium / 0.05m), 0) * 0.05m;
			}

			catch (Exception exception)
			{
				ErrorLogAdapter.Current.LogError(exception);
			}
			return response;
		}
		#endregion "GetPremRateSLU | St. Lucia"

		#region "GetPremRateGRE | Grenada"
		private PremiumResponse GetPremRateGRE(PremiumRequest request)
		{
			req = request;
			PremVar premVar = null;
			PremiumResponse response = new PremiumResponse();
			decimal vehvalue = request.VehicleValue;
			string coverage;
			decimal tmpBasicPremium = 0;


			int vehiclevalue = Convert.ToInt32(request.VehicleValue);


			try
			{
				coverage = request.Coverage;
				//if (coverage == "C") ;
				List<Sys> sysList = SysRepository.Current.Find("CountryCode like '%" + request.Territory + "%'", "CountryCode");
				premVar = PremVarRepository.Current.GetByIDCoverageTerritory(request.VehicleUse, coverage, request.Territory)[0];
				decimal minPrem = Convert.ToDecimal(premVar.Charge);

				if (request.NoOverride != true)
				{

					#region "Comprehensive"
					if (request.Coverage != "T") // Comprehensive Class Coverage
					{

						string tsql = "CountryCode ='" + request.Territory + "' and ObjectCategory = '" + request.VehicleUse + "' and RangeFrom <= " + request.VehicleValue + " and RangeTo >= " + request.VehicleValue + "";
						List<ComprehensiveRate> compPercentGRE = ComprehensiveRateRepository.Current.Find(tsql, "PremiumAmount");
						if (compPercentGRE == null)
						{
							return response;
						}
						tmpBasicPremium = Convert.ToDecimal(compPercentGRE[0].Premiumamount) * request.VehicleValue / 100;
						//Bus Rates use 9-15% of the Vehicle Value
						if (request.VehicleUse == "BS")
						{
							tmpBasicPremium = (request.VehicleValue / 100) * request.BasicPremium;
						}
						if (request.Rental)
						{
							tmpBasicPremium = tmpBasicPremium + Math.Round(tmpBasicPremium * System.Convert.ToDecimal(premVar.Rentalperc / 100), 2, MidpointRounding.AwayFromZero);
						}
						//premium can't be less than minimum premium
						if (tmpBasicPremium < minPrem) tmpBasicPremium = minPrem;

						//TPFT premium = 5% of SumInsured

						if (request.Coverage == "TF")
						{
							if (request.VehicleValue < 50000) tmpBasicPremium = request.VehicleValue * Convert.ToDecimal(0.045); //4.50%
							if (request.VehicleValue > 50000) tmpBasicPremium = request.VehicleValue * Convert.ToDecimal(0.050); //5.00%
						}
					}

					#endregion "Comprehensive"

					#region "ThirdParty"
					if (request.Coverage == "T")
					{
						BindingList<ThirdPartyRates> tp = ThirdPartyRatesAdapter.Current.Find(p => p.RatesCategorie == request.VehicleUse && p.ObjectCategorie == request.EngineSizeText);
						if (tp == null)
						{
							return response;
						}
						tmpBasicPremium = Convert.ToDecimal(tp[0].Amount);
						if (tmpBasicPremium < minPrem) tmpBasicPremium = minPrem;
					}
					response.BasicPremium = tmpBasicPremium;
					#endregion "ThirdParty"

				}
				else
				{
					response.BasicPremium = request.BasicPremium;
				}
				//Added on 23-Oct-2023 By Vincent
				// There will be a premium increase as per 1 Nov for NEW policies and 1-Jan for REN/APR Policies
				DSRateIncrease = Getdataset();
				if (DSRateIncrease != null)
				{
					rateIncrease = Convert.ToDecimal(DSRateIncrease.Tables[0].Rows[0]["Rate"].ToString());
					response.BasicPremium += response.BasicPremium * rateIncrease / 100;
				}

				if (request.NewVehDiscount)
				{
					response.BasicPremium = Math.Round(response.BasicPremium * sysList[0].Newvehdisc, 2, MidpointRounding.AwayFromZero);
				}
				if (request.RateUp > 0)
				{
					response.RateUpAmt = Math.Round(response.BasicPremium * System.Convert.ToDecimal(request.RateUp / 100), 2, MidpointRounding.AwayFromZero);
					response.RateUP = Convert.ToDecimal(request.RateUp);
					//response.BasicPremium = Math.Round(response.BasicPremium + response.RateUpAmt, 2, MidpointRounding.AwayFromZero);
					tmpBasicPremium = Math.Round(response.BasicPremium + response.RateUpAmt, 2, MidpointRounding.AwayFromZero);
				}
				else
				{
					tmpBasicPremium = Math.Round(response.BasicPremium, 2, MidpointRounding.AwayFromZero);
				}

				if (request.Rental == false)
				{
					if (request.AALD)
					{

						response.AALD = Math.Round(tmpBasicPremium * System.Convert.ToDecimal(premVar.Aald / 100), 2, MidpointRounding.AwayFromZero);
						tmpBasicPremium = Math.Round(tmpBasicPremium + response.AALD, 2, MidpointRounding.AwayFromZero);
					}
				}

				// Calc Windscreen Glass Cover (WS)
				if (request.Windscreen && request.Coverage != "T")
				{
					string tsql = "CountryCode ='" + request.Territory + "' and ObjectCategory = 'WS' and RangeFrom <= " + request.VehicleValue + " and RangeTo >= " + request.VehicleValue + "";
					List<ComprehensiveRate> compWS = ComprehensiveRateRepository.Current.Find(tsql, "PremiumAmount");

					tmpBasicPremium = tmpBasicPremium + compWS[0].Premiumamount;
					response.Windscreen = compWS[0].Premiumamount;
				}

				// Calc Temp. Replacement Cover (TR)
				if (request.TempVehicle && request.Coverage != "T")
				{
					string tsql = "CountryCode ='" + request.Territory + "' and ObjectCategory = 'TR' and RangeFrom <= " + request.VehicleValue + " and RangeTo >= " + request.VehicleValue + "";
					List<ComprehensiveRate> compTR = ComprehensiveRateRepository.Current.Find(tsql, "PremiumAmount");

					tmpBasicPremium = tmpBasicPremium + compTR[0].Premiumamount;
					response.Windscreen = compTR[0].Premiumamount;
				}
				if (request.ActOfGOD && request.Coverage != "T") //Special Perils
				{
					string tsql = "CountryCode ='" + request.Territory + "' and ObjectCategory = 'SP'";
					List<ComprehensiveRate> compSP = ComprehensiveRateRepository.Current.Find(tsql, "PremiumAmount");
					//response.ActOfGOD = (premVar.Actofgod * vehvalue);
					response.ActOfGOD = (compSP[0].Premiumamount * vehvalue / 100);
					if (response.ActOfGOD < compSP[0].Rangeto) response.ActOfGOD = compSP[0].Rangeto;
					tmpBasicPremium = tmpBasicPremium + response.ActOfGOD;
				}


				response.AddTPL = request.AddTPL;
				response.ToolsOfTrade = request.ToolsOfTrade;
				tmpBasicPremium = Math.Round(tmpBasicPremium + response.AddTPL + response.ToolsOfTrade, 2, MidpointRounding.AwayFromZero);
				if (request.FleetDiscount)
				{
					response.Fleetdisc = Math.Round(tmpBasicPremium * System.Convert.ToDecimal(premVar.Fleetdisc / 100), 2, MidpointRounding.AwayFromZero);
					tmpBasicPremium = Math.Round(tmpBasicPremium - response.Fleetdisc, 2, MidpointRounding.AwayFromZero);
				}
				if (request.StaffDiscount)
				{
					response.StaffDisc = Math.Round(tmpBasicPremium * System.Convert.ToDecimal(premVar.Staffdisc / 100), 2, MidpointRounding.AwayFromZero);
					tmpBasicPremium = Math.Round(tmpBasicPremium - response.StaffDisc, 2, MidpointRounding.AwayFromZero);
				}

				tmpBasicPremium += response.ExtraLoads;
				if (request.ManagDisc > 0)
				{
					response.ManagDisc = Math.Round(tmpBasicPremium * System.Convert.ToDecimal(request.ManagDisc / 100), 2, MidpointRounding.AwayFromZero);
					tmpBasicPremium = Math.Round(tmpBasicPremium - response.ManagDisc, 2, MidpointRounding.AwayFromZero);
				}

				response.YearPremium = tmpBasicPremium;
				response.GrossPremium = response.YearPremium;

				if (request.Period < 12 && request.ShortPeriod == false)
				{
					response.GrossPremium = Math.Round(response.YearPremium * System.Convert.ToDecimal(request.RateCode / 100), 2, MidpointRounding.AwayFromZero);
					switch (request.PremiumRateCode)
					{
						case "1Q":
							response.GrossPremium = Math.Round(response.YearPremium * System.Convert.ToDecimal(premVar.Quarterlyperc / 100), 2, MidpointRounding.AwayFromZero);
							break;
						case "3M":
							response.GrossPremium = Math.Round(response.YearPremium * System.Convert.ToDecimal(0.4), 2, MidpointRounding.AwayFromZero);
							break;
						case "9M":
							response.GrossPremium = Math.Round(response.YearPremium * System.Convert.ToDecimal(0.6), 2, MidpointRounding.AwayFromZero);
							break;
						case "6M1":
							response.GrossPremium = Math.Round(response.YearPremium * System.Convert.ToDecimal(0.7), 2, MidpointRounding.AwayFromZero);
							break;
						case "6M2":
							response.GrossPremium = Math.Round(response.YearPremium * System.Convert.ToDecimal(0.3), 2, MidpointRounding.AwayFromZero);
							break;
					}
				}
				if (request.ShortPeriod)
				{
					response.GrossPremium = (response.YearPremium * request.ShortPeriod_Perc) / 100;
				}
				if (request.NCD != 0) response.NCD = Math.Round(response.GrossPremium * System.Convert.ToDecimal(request.NCD / 100), 2, MidpointRounding.AwayFromZero);

				response.DiscountPremium = Math.Round(response.GrossPremium - response.NCD, 2, MidpointRounding.AwayFromZero);
				if (request.PassLiab > 0)
				{
					response.PassLiab = Math.Round(System.Convert.ToDecimal(request.PassLiab * premVar.Passliab), 2, MidpointRounding.AwayFromZero);
					if (request.ShortPeriod == false)
					{
						switch (request.PremiumRateCode)
						{
							case "1Q":
								response.PassLiab = Math.Round((response.PassLiab * System.Convert.ToDecimal(premVar.Quarterlyperc / 100)), 2, MidpointRounding.AwayFromZero);
								break;
							case "3M":
								response.PassLiab = Math.Round(response.PassLiab * System.Convert.ToDecimal(0.4), 2, MidpointRounding.AwayFromZero);
								break;
							case "9M":
								response.PassLiab = Math.Round(response.PassLiab * System.Convert.ToDecimal(0.4), 2, MidpointRounding.AwayFromZero);
								break;
							case "6M1":
								response.PassLiab = Math.Round(response.PassLiab * System.Convert.ToDecimal(0.7), 2, MidpointRounding.AwayFromZero);
								break;
							case "6M2":
								response.PassLiab = Math.Round(response.PassLiab * System.Convert.ToDecimal(0.3), 2, MidpointRounding.AwayFromZero);
								break;
						}
					}
					else
					{
						response.PassLiab = Math.Round((response.PassLiab * System.Convert.ToDecimal(request.ShortPeriod_Perc / 100)), 2, MidpointRounding.AwayFromZero);
					}
				}
				if (request.isPercentageCampaign) response.CampaignAmt = ((response.DiscountPremium - response.NewVehDiscValue + response.PassLiab + request.ExtraCoverage1 + request.ExtraCoverage2) * request.Campaigns) / 100; else response.CampaignAmt = request.Campaigns;

				//New request, round to the nearest $0.05 as per 1 July 2015
				response.NetPremium = Math.Round(response.DiscountPremium + response.PassLiab + request.ExtraCoverage1 + request.ExtraCoverage2 + response.CampaignAmt, 2, MidpointRounding.AwayFromZero);
				response.NetPremium = Math.Round((response.NetPremium / 0.05m), 0) * 0.05m;
				response.PolicyFee = request.Policyfee;
				response.PolicyFee = Math.Round((response.PolicyFee / 0.05m), 0) * 0.05m;
				response.GovTax = response.NetPremium * request.GovTax;
				response.GovTax = Math.Round((response.GovTax / 0.05m), 0) * 0.05m;
				response.TotalPremium = Math.Round(response.NetPremium, 2, MidpointRounding.AwayFromZero) + response.PolicyFee + Math.Round(response.GovTax, 2, MidpointRounding.AwayFromZero);
				response.TotalPremium = Math.Round((response.TotalPremium / 0.05m), 0) * 0.05m;
			}

			catch (Exception exception)
			{
				ErrorLogAdapter.Current.LogError(exception);
			}
			return response;
		}
		#endregion "GetPremRateGRE | Grenada"

		#region "GetPremRateSVC | St. Vincent"

		private PremiumResponse GetPremRateSVC(PremiumRequest request)
		{
			//CompRate premium = null;
			PremVar premVar = null;
			PremiumResponse response = new PremiumResponse();
			decimal vehvalue = request.VehicleValue;
			string coverage;
			decimal tmpBasicPremium = 0;
			decimal addcharge = 0.0m;

			int vehiclevalue = Convert.ToInt32(request.VehicleValue);
			int newVehValue = 0;

			try
			{
				coverage = request.Coverage;
				//if (coverage == "C") ;
				List<Sys> sysList = SysRepository.Current.Find("CountryCode like '%" + request.Territory + "%'", "CountryCode");
				premVar = PremVarRepository.Current.GetByIDCoverageTerritory(request.VehicleUse, coverage, request.Territory)[0];
				List<TransactionRate> tr = TransactionRateRepository.Current.Find("RateCode = '" + request.PremiumRateCode + "'", "RateCode");

				List<NVehUse> vehUses = NVehUseRepository.Current.Find("VUseID = '" + request.VehicleUse + "' AND Coverage ='" + request.Coverage + "'", "VUseID");
				if (vehUses == null || vehUses.Count == 0) return response;
				tmpBasicPremium = Convert.ToDecimal(vehUses[0].Premium);

				//Get The Premium Rates from the nVehUse Table
				if (request.NoOverride != true)
				{
					if (vehvalue < vehUses[0].MinSumIns)
					{
						response.errorMessage = "A minimum value of " + vehUses[0].MinSumIns + " is required for " + request.CoverageText + " coverage";
						return response;
					}
					if (vehvalue > vehUses[0].TreshHold)
					{
						addcharge = System.Math.Round((Convert.ToDecimal(vehvalue - vehUses[0].TreshHold) / 1000), 2, MidpointRounding.AwayFromZero) * vehUses[0].AddCharge;
					}
					if (request.Coverage == "TF")
					{
						addcharge = vehUses[0].AddCharge * (vehUses[0].Loadperc * (request.VehicleValue - vehUses[0].TreshHold) / 100); //$30 * 0.1% of Veh. Value

					}


					tmpBasicPremium += addcharge;
					response.BasicPremium = tmpBasicPremium;
				}
				else
				{
					response.BasicPremium = request.BasicPremium;
				}
				if (request.NewVehDiscount)
				{
					response.BasicPremium = Math.Round(response.BasicPremium * sysList[0].Newvehdisc, 2, MidpointRounding.AwayFromZero);
				}
				if (request.RateUp > 0)
				{
					response.RateUpAmt = Math.Round(response.BasicPremium * System.Convert.ToDecimal(request.RateUp / 100), 2, MidpointRounding.AwayFromZero);
					response.RateUP = Convert.ToDecimal(request.RateUp);
					//response.BasicPremium = Math.Round(response.BasicPremium + response.RateUpAmt, 2, MidpointRounding.AwayFromZero);
					tmpBasicPremium = Math.Round(response.BasicPremium + response.RateUpAmt, 2, MidpointRounding.AwayFromZero);
				}
				else
				{
					tmpBasicPremium = Math.Round(response.BasicPremium, 2, MidpointRounding.AwayFromZero);
				}

				if (request.AALD)
				{
					response.AALD = Math.Round(tmpBasicPremium * System.Convert.ToDecimal(premVar.Aald / 100), 2, MidpointRounding.AwayFromZero);
					tmpBasicPremium = Math.Round(tmpBasicPremium + response.AALD, 2, MidpointRounding.AwayFromZero);
				}


				// Calc Temp. Replacement Cover (TR)
				if (request.TempVehicle && request.Coverage != "T")
				{
					string tsql = "CountryCode ='" + request.Territory + "' and ObjectCategory = 'TR' and RangeFrom <= " + request.VehicleValue + " and RangeTo >= " + request.VehicleValue + "";
					List<ComprehensiveRate> compTR = ComprehensiveRateRepository.Current.Find(tsql, "PremiumAmount");

					tmpBasicPremium = tmpBasicPremium + compTR[0].Premiumamount;
					response.Windscreen = compTR[0].Premiumamount;
				}

				response.AddTPL = request.AddTPL;
				//response.ToolsOfTrade = request.ToolsOfTrade;
				tmpBasicPremium = Math.Round(tmpBasicPremium + response.AddTPL + response.ToolsOfTrade, 2, MidpointRounding.AwayFromZero);
				if (request.FleetDiscount)
				{
					response.Fleetdisc = Math.Round(tmpBasicPremium * System.Convert.ToDecimal(premVar.Fleetdisc / 100), 2, MidpointRounding.AwayFromZero);
					tmpBasicPremium = Math.Round(tmpBasicPremium - response.Fleetdisc, 2, MidpointRounding.AwayFromZero);
				}
				if (request.StaffDiscount)
				{
					response.StaffDisc = Math.Round(tmpBasicPremium * System.Convert.ToDecimal(premVar.Staffdisc / 100), 2, MidpointRounding.AwayFromZero);
					tmpBasicPremium = Math.Round(tmpBasicPremium - response.StaffDisc, 2, MidpointRounding.AwayFromZero);
				}

				tmpBasicPremium += response.ExtraLoads;
				if (request.ManagDisc > 0)
				{
					response.ManagDisc = Math.Round(tmpBasicPremium * System.Convert.ToDecimal(request.ManagDisc / 100), 2, MidpointRounding.AwayFromZero);
					tmpBasicPremium = Math.Round(tmpBasicPremium - response.ManagDisc, 2, MidpointRounding.AwayFromZero);
				}

				response.YearPremium = tmpBasicPremium;
				response.GrossPremium = response.YearPremium;

				if (request.Period < 12 && request.ShortPeriod == false)
				{
					response.GrossPremium = Math.Round(response.YearPremium * System.Convert.ToDecimal(request.RateCode / 100), 2, MidpointRounding.AwayFromZero);
					switch (request.PremiumRateCode)
					{
						case "3M":    // 1st 3 Months coverage
							response.GrossPremium = Math.Round(response.YearPremium * System.Convert.ToDecimal(premVar.Quarterlyperc / 100), 2, MidpointRounding.AwayFromZero);
							break;
						case "6M":      // 6 Month rate
							response.GrossPremium = Math.Round(response.YearPremium * System.Convert.ToDecimal(premVar.Halfyearlyperc / 100), 2, MidpointRounding.AwayFromZero);
							break;
					}
				}
				if (request.ShortPeriod == true)
				{
					response.GrossPremium = (response.YearPremium * request.ShortPeriod_Perc) / 100;
				}
				if (request.NCD != 0) response.NCD = Math.Round(response.GrossPremium * System.Convert.ToDecimal(request.NCD / 100), 2, MidpointRounding.AwayFromZero);



				response.DiscountPremium = Math.Round(response.GrossPremium - response.NCD, 2, MidpointRounding.AwayFromZero);
				if (request.PassLiab > 0)
				{
					response.PassLiab = Math.Round(System.Convert.ToDecimal(request.PassLiab * premVar.Passliab), 2, MidpointRounding.AwayFromZero);
					//if (request.ShortPeriod == false)
					//{
					//	switch (request.PremiumRateCode)
					//	{
					//		case "1Q":
					//			response.PassLiab = Math.Round((response.PassLiab * System.Convert.ToDecimal(premVar.Quarterlyperc / 100)), 2, MidpointRounding.AwayFromZero);
					//			break;
					//		case "3M50":
					//			response.PassLiab = Math.Round(response.PassLiab / tr[0].Netdivider, 2, MidpointRounding.AwayFromZero);
					//			break;
					//		case "6M":
					//			response.PassLiab = Math.Round((response.PassLiab * System.Convert.ToDecimal(premVar.Halfyearlyperc / 100)), 2, MidpointRounding.AwayFromZero);
					//			break;
					//		case "9M50":
					//			response.PassLiab = Math.Round(response.PassLiab / tr[0].Netdivider, 2, MidpointRounding.AwayFromZero);
					//			break;
					//	}
					//}
					//else
					//{
					//	response.PassLiab = Math.Round((response.PassLiab * System.Convert.ToDecimal(request.ShortPeriod_Perc / 100)), 2, MidpointRounding.AwayFromZero);
					//}
				}



				if (request.isPercentageCampaign) response.CampaignAmt = ((response.DiscountPremium - response.NewVehDiscValue + request.ExtraCoverage1 + request.ExtraCoverage2) * request.Campaigns) / 100; else response.CampaignAmt = request.Campaigns;

				if (request.ActOfGOD && request.Coverage != "T")
				{
					if (request.NoChargeAOG)
					{
						response.Windscreen = premVar.Actofgod * vehvalue / 1000;
						response.ActOfGOD = 0;
					}
					else
					{
						response.Windscreen = 0;
						response.ActOfGOD = premVar.Actofgod * vehvalue / 1000; ////$5 for every $1000
					}
					response.DiscountPremium = response.DiscountPremium + response.ActOfGOD + response.PassLiab;
				}
				//For tractors, charge toolsoftrade extra
				response.DiscountPremium += vehUses[0].ToolsCharge;
				response.ToolsOfTrade = vehUses[0].ToolsCharge;

				request.GovTax = sysList[0].Tax;
				//New request, round to the nearest $0.05 as per 1 July 2015
				response.NetPremium = Math.Round(response.DiscountPremium + response.PassLiab + request.ExtraCoverage1 + request.ExtraCoverage2 + response.CampaignAmt, 2, MidpointRounding.AwayFromZero);
				response.NetPremium = Math.Round((response.NetPremium / 0.05m), 0) * 0.05m;
				response.PolicyFee = request.Policyfee;
				response.PolicyFee = Math.Round((response.PolicyFee / 0.05m), 0) * 0.05m;
				response.GovTax = response.NetPremium * request.GovTax;
				response.GovTax = Math.Round((response.GovTax / 0.05m), 0) * 0.05m;
				response.TotalPremium = Math.Round(response.NetPremium, 2, MidpointRounding.AwayFromZero) + response.PolicyFee + Math.Round(response.GovTax, 2, MidpointRounding.AwayFromZero);
				response.TotalPremium = Math.Round((response.TotalPremium / 0.05m), 0) * 0.05m;
			}

			catch (Exception exception)
			{
				ErrorLogAdapter.Current.LogError(exception);
			}
			return response;
		}
		#endregion "GetPremRateSVC | St. Vincent"

		#region "GetPremRateTCI | Turks and Caicos"
		private PremiumResponse GetPremRateTCI(PremiumRequest request)
		{
			req = request;
			PremVar premVar = null;
			PremiumResponse response = new PremiumResponse();
			decimal vehvalue = request.VehicleValue;
			string coverage;
			decimal tmpBasicPremium = 0;
			decimal addcharge = 0.0m;

			int vehiclevalue = Convert.ToInt32(request.VehicleValue);
			int newVehValue = 0;

			try
			{
				coverage = request.Coverage;
				//if (coverage == "C") ;
				List<Sys> sysList = SysRepository.Current.Find("CountryCode like '%" + request.Territory + "%'", "CountryCode");
				premVar = PremVarRepository.Current.GetByIDCoverageTerritory(request.VehicleUse, coverage, request.Territory)[0];
				List<TransactionRate> tr = TransactionRateRepository.Current.Find("RateCode = '" + request.PremiumRateCode + "'", "RateCode");

				List<NVehUse> vehUses = NVehUseRepository.Current.Find("VUseID = '" + request.VehicleUse + "' AND Coverage ='" + request.Coverage + "'", "VUseID");
				if (vehUses == null || vehUses.Count == 0) return response;
				tmpBasicPremium = Convert.ToDecimal(vehUses[0].Premium);

				//Get The Premium Rates from the nVehUse Table
				if (request.NoOverride == false)
				{


					addcharge = (vehUses[0].Loadperc * request.VehicleValue / 100);

					//For tractors, charge toolsoftrade extra
					addcharge += vehUses[0].ToolsCharge;
					tmpBasicPremium += addcharge;
					response.BasicPremium = tmpBasicPremium;
				}
				else
				{
					response.BasicPremium = request.BasicPremium;
				}

				//Added on 23-Oct-2023 By Vincent
				// There will be a premium increase as per 1 Nov for NEW policies and 1-Jan for REN/APR Policies
				DSRateIncrease = Getdataset();
				//if (DSRateIncrease != null)
				//{
				//	rateIncrease = Convert.ToDecimal(DSRateIncrease.Tables[0].Rows[0]["Rate"].ToString());
				//	response.BasicPremium += response.BasicPremium * rateIncrease / 100;
				//}

				if (DSRateIncrease != null)
				{
					rateIncrease = Convert.ToDecimal(DSRateIncrease.Tables[0].Rows[0]["Rate"].ToString());
					//only use the new premium if the policy had its full year
					if (request.NCDDate == null) request.NCDDate = DateTime.Today;
					if (request.EffectiveDate == null) request.EffectiveDate = DateTime.Today;
					if (request.ApplicationDate == null) request.ApplicationDate = request.EffectiveDate;
					if (request.VehicleUse == "MB" || request.VehicleUse == "MC")
					{
						response.BasicPremium = response.BasicPremium;
					}
					else if (request.NoOverride == true)
					{
						response.BasicPremium = request.BasicPremium;
					}
					else if (request.ApplicationDate > Convert.ToDateTime("11/15/2023") && request.IsNew == false)
					{
						response.BasicPremium = response.BasicPremium += response.BasicPremium * rateIncrease / 100;
					}
					else
					{
						response.BasicPremium = response.BasicPremium += response.BasicPremium * rateIncrease / 100;
					}

					if (request.NewVehDiscount)
					{
						response.BasicPremium = Math.Round(response.BasicPremium * sysList[0].Newvehdisc, 2, MidpointRounding.AwayFromZero);
					}
				}
				//Rateup based on Age
				List<AgeCategory> agegroup = AgeCategoryController.Find("Description = '" + request.AgeGroup + "'", "Description");
				if (agegroup != null && agegroup.Count > 0)
				{
					response.BasicPremium = response.BasicPremium * Convert.ToDecimal(agegroup[0].Value);
				}

				if (request.RateUp > 0)
				{
					response.RateUpAmt = Math.Round(response.BasicPremium * System.Convert.ToDecimal(request.RateUp / 100), 2, MidpointRounding.AwayFromZero);
					response.RateUP = Convert.ToDecimal(request.RateUp);
					//response.BasicPremium = Math.Round(response.BasicPremium + response.RateUpAmt, 2, MidpointRounding.AwayFromZero);
					tmpBasicPremium = Math.Round(response.BasicPremium + response.RateUpAmt, 2, MidpointRounding.AwayFromZero);
				}
				else
				{
					tmpBasicPremium = Math.Round(response.BasicPremium, 2, MidpointRounding.AwayFromZero);
				}

				if (request.AALD)
				{
					response.AALD = Math.Round(tmpBasicPremium * System.Convert.ToDecimal(premVar.Aald / 100), 2, MidpointRounding.AwayFromZero);
					tmpBasicPremium = Math.Round(tmpBasicPremium + response.AALD, 2, MidpointRounding.AwayFromZero);
				}

				//if (request.ActOfGOD && request.Coverage != "T") //Special Perils
				//{
				//	string tsql = "CountryCode ='" + request.Territory + "' and ObjectCategory = 'SP'";
				//	List<ComprehensiveRate> compSP = ComprehensiveRateRepository.Current.Find(tsql, "PremiumAmount");
				//	//response.ActOfGOD = (premVar.Actofgod * vehvalue);
				//	response.ActOfGOD = (compSP[0].Premiumamount * vehvalue / 100);
				//	if (response.ActOfGOD < compSP[0].Rangeto) response.ActOfGOD = compSP[0].Rangeto;
				//	tmpBasicPremium = tmpBasicPremium + response.ActOfGOD;
				//}

				response.AddTPL = request.AddTPL;
				response.ToolsOfTrade = request.ToolsOfTrade;
				tmpBasicPremium = Math.Round(tmpBasicPremium + response.AddTPL + response.ToolsOfTrade, 2, MidpointRounding.AwayFromZero);
				if (request.FleetDiscount)
				{
					response.Fleetdisc = Math.Round(tmpBasicPremium * System.Convert.ToDecimal(premVar.Fleetdisc / 100), 2, MidpointRounding.AwayFromZero);
					tmpBasicPremium = Math.Round(tmpBasicPremium - response.Fleetdisc, 2, MidpointRounding.AwayFromZero);
				}
				if (request.StaffDiscount)
				{
					response.StaffDisc = Math.Round(tmpBasicPremium * System.Convert.ToDecimal(premVar.Staffdisc / 100), 2, MidpointRounding.AwayFromZero);
					tmpBasicPremium = Math.Round(tmpBasicPremium - response.StaffDisc, 2, MidpointRounding.AwayFromZero);
				}

				tmpBasicPremium += response.ExtraLoads;
				if (request.ManagDisc > 0)
				{
					response.ManagDisc = Math.Round(tmpBasicPremium * System.Convert.ToDecimal(request.ManagDisc / 100), 2, MidpointRounding.AwayFromZero);
					tmpBasicPremium = Math.Round(tmpBasicPremium - response.ManagDisc, 2, MidpointRounding.AwayFromZero);
				}

				response.YearPremium = tmpBasicPremium;
				response.GrossPremium = response.YearPremium;

				if (request.Period < 12 && request.ShortPeriod == false)
				{
					response.GrossPremium = Math.Round(response.YearPremium * System.Convert.ToDecimal(request.RateCode / 100), 2, MidpointRounding.AwayFromZero);
					switch (request.PremiumRateCode)
					{
						case "1Q":      //Quarterly rate
							response.GrossPremium = Math.Round(response.YearPremium * System.Convert.ToDecimal(premVar.Quarterlyperc / 100), 2, MidpointRounding.AwayFromZero);
							break;
						case "3M50":    // 1st 3 Months coverage
							response.GrossPremium = Math.Round(response.YearPremium / tr[0].Netdivider, 2, MidpointRounding.AwayFromZero);
							break;
						case "6M":      // 6 Month rate
							response.GrossPremium = Math.Round(response.YearPremium * System.Convert.ToDecimal(premVar.Halfyearlyperc / 100), 2, MidpointRounding.AwayFromZero);
							break;
						case "9M50":    //9 Months coverage (2nd part of the 3months coverage rate)
							response.GrossPremium = Math.Round(response.YearPremium / tr[0].Netdivider, 2, MidpointRounding.AwayFromZero);
							break;
					}
				}
				if (request.ShortPeriod == true)
				{
					response.GrossPremium = (response.YearPremium * request.ShortPeriod_Perc) / 100;
				}
				if (request.NCD != 0) response.NCD = Math.Round(response.GrossPremium * System.Convert.ToDecimal(request.NCD / 100), 2, MidpointRounding.AwayFromZero);



				response.DiscountPremium = Math.Round(response.GrossPremium - response.NCD, 2, MidpointRounding.AwayFromZero);
				if (request.PassLiab > 0)
				{
					response.PassLiab = Math.Round(System.Convert.ToDecimal(request.PassLiab * premVar.Passliab), 2, MidpointRounding.AwayFromZero);
					//if (request.ShortPeriod == false)
					//{
					//	switch (request.PremiumRateCode)
					//	{
					//		case "1Q":
					//			response.PassLiab = Math.Round((response.PassLiab * System.Convert.ToDecimal(premVar.Quarterlyperc / 100)), 2, MidpointRounding.AwayFromZero);
					//			break;
					//		case "3M50":
					//			response.PassLiab = Math.Round(response.PassLiab / tr[0].Netdivider, 2, MidpointRounding.AwayFromZero);
					//			break;
					//		case "6M":
					//			response.PassLiab = Math.Round((response.PassLiab * System.Convert.ToDecimal(premVar.Halfyearlyperc / 100)), 2, MidpointRounding.AwayFromZero);
					//			break;
					//		case "9M50":
					//			response.PassLiab = Math.Round(response.PassLiab / tr[0].Netdivider, 2, MidpointRounding.AwayFromZero);
					//			break;
					//	}
					//}
					//else
					//{
					//	response.PassLiab = Math.Round((response.PassLiab * System.Convert.ToDecimal(request.ShortPeriod_Perc / 100)), 2, MidpointRounding.AwayFromZero);
					//}
				}



				if (request.isPercentageCampaign)
				{
					response.CampaignAmt = ((response.DiscountPremium - response.NewVehDiscValue + response.PassLiab + request.ExtraCoverage1 + request.ExtraCoverage2) * request.Campaigns) / 100;
				}
				else
				{
					response.CampaignAmt = request.Campaigns;
				}


				request.GovTax = sysList[0].Tax;
				//New request, round to the nearest $0.05 as per 1 July 2015
				response.NetPremium = Math.Round(response.DiscountPremium + response.PassLiab + request.ExtraCoverage1 + request.ExtraCoverage2 + response.CampaignAmt, 2, MidpointRounding.AwayFromZero);
				response.NetPremium = Math.Round((response.NetPremium / 0.05m), 0) * 0.05m;
				response.PolicyFee = request.Policyfee;
				response.PolicyFee = Math.Round((response.PolicyFee / 0.05m), 0) * 0.05m;
				response.GovTax = response.NetPremium * request.GovTax;
				response.GovTax = Math.Round((response.GovTax / 0.05m), 0) * 0.05m;
				response.TotalPremium = Math.Round(response.NetPremium, 2, MidpointRounding.AwayFromZero) + response.PolicyFee + Math.Round(response.GovTax, 2, MidpointRounding.AwayFromZero);
				response.TotalPremium = Math.Round((response.TotalPremium / 0.05m), 0) * 0.05m;
			}

			catch (Exception exception)
			{
				ErrorLogAdapter.Current.LogError(exception);
				response.errorMessage = exception.ToString();
			}
			return response;
		}
		#endregion "GetPremRateTCI | Turks 7 Caicos"

		#region "GetPremRateBAH | Bahamas"
		private PremiumResponse GetPremRateBAH(PremiumRequest request)
		{
			PremiumResponse response = new PremiumResponse();
			List<VehiclePremiumRates> currentRates = new List<VehiclePremiumRates>();
			List<VehiclePremiumRates> allRates;
			List<VehiclePremiumRates> ageRates;
			Policy policy = new Policy();
			policy.PremiumRatesCollection = PolicyPremiumRatesController.GetByPolicyNo(request.PolicyNo);
			Inspro.Vehicle vehicle;

			List<Inspro.Coverage> coverages;
			List<Inspro.NCD> ncdPremList = null;
			decimal basicprem = request.BasicPremium; //needs to be exposed to the caller form
			decimal minPremium = 0.0m;
			decimal minRateValue = 0.0m;
			decimal totalRateUpDiscount = 0.0m;
			decimal total = 0.0m;
			decimal ncdamt = 0.0m;
			decimal yrpremium = 0.0m;
			decimal grspremium = request.Grosspremium;
			decimal netpremium = 0.0m; ;
			decimal totalplustax = 0.0m;
			bool isMinPremium = false;
			int totalLoadingPerc = 0;
			decimal govtax = 0.0m;
			decimal govvat = 0.0m;
			decimal ncd = 0;
			bool lossofuse = false;
			bool ncdprotect = false;
			bool nochanges = false; //in case the user clicks cancel, the values don't need to be reset
									//string MasterAgentCode = request.
									// internal List<Sys> sysList;
			decimal min_CompRate = 0.0m;  // Minimum Comprehensive  Rate
			decimal min_ThirdPRate = 0.0m; // Minimum Thirdparty  Rate
			decimal ComprBasisValue = 50000m; // Max value is $40,000.00
			decimal ComprCommBasisValue = 40000m; // Max value is $40,000.00
			decimal PercOfRate = 0.40m; // Apply 50% of the Rate to the amount in excess - 
			decimal PercOfCommRate = 0.05m; // Apply 5% to the amount that is in excess of $40,000  - Commecrial
			DateTime effdate = DateTime.Now;
			DateTime NCDDate = DateTime.Now;
			bool IsNew = false;
			decimal vehvalue = request.VehicleValue;
			decimal extravalue = 0.0m;
			decimal extrapremium = 0.0m;
			decimal vehrate = 0.0m;
			req = request;

			try
			{
				if (request.NoOverride == true)
				{
					response.BasicPremium = request.BasicPremium;
					response.GrossPremium = request.Grosspremium;
					response.NetPremium = request.Grosspremium;
					response.NCD = request.NCD;
					return response;
				}

				List<VehiclePremiumRates> MinRate = VehiclePremiumRatesController.Find("RateCode = '" + request.VehicleUse + "' AND RateCategory = 'MinRate'", "RateCode");
				if (MinRate.Count > 0)
				{
					min_CompRate = MinRate[0].Comprehensive;
					min_ThirdPRate = MinRate[0].ThirdParty;

				}
				VehiclePremiumRates agerate = VehiclePremiumRatesController.Find("RateCode = '" + request.AgeCat + "' AND RateCategory = 'Premium'", "RateCode")[0];

				if (request.VehicleUse == "PR")
				{
					if (request.Coverage == "C")
					{
						List<VehiclePremiumRates> ComprBasisValueRate = VehiclePremiumRatesController.Find("RateCode = '" + request.VehicleUse + "' AND RateCategory = 'BaseRate'", "RateCode");
						ComprBasisValue = ComprBasisValueRate[0].Comprehensive;
						PercOfRate = ComprBasisValueRate[0].RatePerc;
						minPremium = ComprBasisValueRate[0].Comprehensive;
						vehrate = agerate.Comprehensive;
						if (vehvalue <= ComprBasisValue)
						{
							basicprem = vehvalue * vehrate / 100;
						}
						else
						{
							if (request.MasterAgentCode != "NON") //this user needs to get permission first from NAGICO for vehicles greater than $50,000
							{
								MessageBox.Show("Please contact NAGICO's Office for a quote on a vehicle with a value greater than $50,000.00");
								return response;
							}
							extravalue = vehvalue - ComprBasisValue;
							decimal extraprem = extravalue * (vehrate * PercOfRate) / 100;
							decimal basisvalprem = ComprBasisValue * vehrate / 100;
							basicprem = basisvalprem + extraprem;
						}
						if (basicprem < min_CompRate)
						{
							basicprem = min_CompRate;
						}
						//Use if overrideminimum premium was checked
						if (minRateValue != 0) basicprem = minRateValue;
					}
					else if (request.Coverage == "T")
					{
						vehrate = agerate.ThirdParty;
						minPremium = min_ThirdPRate;
						basicprem = vehrate;
						if (basicprem < min_ThirdPRate)
						{
							basicprem = min_ThirdPRate;
						}
					}
				}
				else // Commercial Calculation
				{
					List<VehiclePremiumRates> ComprCommBasisValueRate = VehiclePremiumRatesController.Find("RateCode = '" + request.VehicleUse + "' AND RateCategory = 'BaseRate'", "RateCode");
					ComprCommBasisValue = ComprCommBasisValueRate[0].Comprehensive;
					PercOfCommRate = ComprCommBasisValueRate[0].RatePerc;

					if (vehvalue > ComprCommBasisValue)
					{
						if (request.MasterAgentCode != "NON") //this user needs to get permission first from NAGICO for vehicles greater than $50,000
						{
							MessageBox.Show("Please contact NAGICO's Office for a quote on a vehicle with a value greater than $40,000.00");
							return response;
						}
						else
						{
							extravalue = vehvalue - ComprCommBasisValue;
							vehvalue = ComprCommBasisValue;
						}
					}

					List<VehiclePremiumRates> CommList = VehiclePremiumRatesController.Find("RateCode = '" + request.VehicleType + "' AND RatePerc >= " + vehvalue + "", "RateCode");
					if (CommList.Count > 0)
					{
						int i = CommList.Count;
						i = 0;
						if (request.Coverage == "C")
						{

							minPremium = min_CompRate;
							basicprem = CommList[i].Comprehensive;
							//if the vehicle value is higher than the maximum allowed

							if (extravalue > 0) extrapremium = extravalue * PercOfCommRate;
							basicprem = basicprem + extrapremium;
							if (basicprem < min_CompRate)
							{
								basicprem = min_CompRate; //set the miunimum premium values
							}
						}
						else if (request.Coverage == "T")
						{
							minPremium = min_ThirdPRate;
							basicprem = CommList[i].ThirdParty;
							if (basicprem < min_ThirdPRate)
							{
								basicprem = min_ThirdPRate;  //set the miunimum premium values
							}

						}
					}

				}

				//Added on 23-Oct-2023 By Vincent
				// There will be a premium increase as per 1 Nov for NEW policies and 1-Jan for REN/APR Policies
				DSRateIncrease = Getdataset();
				if (DSRateIncrease != null)
				{
					rateIncrease = Convert.ToDecimal(DSRateIncrease.Tables[0].Rows[0]["Rate"].ToString());
					//only use the new premium if the policy had its full year
					if (NCDDate == null) NCDDate = DateTime.Today;
					if (NCDDate <= effdate)
					{
						basicprem += basicprem * rateIncrease / 100;
					}
				}
				response.BasicPremium = basicprem;


				//Use if overrideminimum premium was checked
				if (minRateValue != 0) basicprem = minRateValue;

				//calculateBHMTotal();
				totalLoadingPerc = 0;
				foreach (VehiclePremiumRates vehiclepremiumrates in currentRates)
				{
					{
						totalRateUpDiscount += (vehiclepremiumrates.RatePerc * basicprem / 100);
						totalLoadingPerc += Convert.ToInt16(vehiclepremiumrates.RatePerc);
					}
				}
				ncd = request.NCD;
				total = basicprem + totalRateUpDiscount;
				yrpremium = total;
				grspremium = total;
				if (request.Rental || request.ForLicense) yrpremium = total + 100.0m;
				ncdamt = (total * ncd) / 100;
				total = total - ((total * ncd) / 100); //netpremium
				if (total < minPremium && minRateValue != 0)
				{
					total = minPremium;
					isMinPremium = true;
				}
				response.NCD = ncd;
				response.YearPremium = yrpremium;
				response.GrossPremium = grspremium;
				response.DiscountPremium = grspremium - ncd;
			}

			catch (Exception ex)
			{
				ErrorLogAdapter.Current.LogError(ex);
			}
			CalcNoOverride(request, true);
			return response;
		}

		#endregion "GetPremRateBAH | Bahamas"

		#region "No CalcNoOverrideOverride"
		public PremiumResponse CalcNoOverride(PremiumRequest request, Boolean testOveride)
		{
			Decimal GrsAmt;
			PremiumResponse response = new PremiumResponse();
			decimal tmpBasicPremium = 0;

			List<Sys> sysList = SysRepository.Current.Find("CountryCode like '%" + request.Territory + "%'", "CountryCode");
			if (sysList.Count == 0)
			{
				//set default values to 0
				sysList = new List<Sys>();
				sysList.Add(new Sys()
				{
					Tax = 0,
					Policyfee = 0
				}
				);

			}

			try
			{


				if (request.NoOverride && request.DontOverrideNVD == false)
				{
					response.NewVehDiscPerc = 0;
					response.NewVehDiscValue = 0;
				}
				//For Non Bahamas Islands only
				//if (request.Territory != "BAH" && request.Territory != "TCI")
				//{
				//	response.RateUP = Math.Round(request.BasicPremium * System.Convert.ToDecimal(request.RateUp / 100), 2, MidpointRounding.AwayFromZero);
				//	response.RateUpAmt = response.RateUP;
				//}
				//else
				
					response.RateUP = Convert.ToDecimal(request.RateUp); // Loading Rate Percentage
					//response.RateUpAmt = Convert.ToDecimal(request.RateUpAmt);
					response.RateUpAmt= Math.Round(request.BasicPremium * System.Convert.ToDecimal(request.RateUp / 100), 2, MidpointRounding.AwayFromZero);
				
				tmpBasicPremium = Math.Round(request.BasicPremium, 2, MidpointRounding.AwayFromZero);

				//For ABC Islands only
				if (request.Territory == "ARU" || request.Territory == "BON" || request.Territory == "CUR")
				{
					response.AddTPL = 0;
					List<LiabilityVar> liabilityVarList1 = LiabilityVarRepository.Current.Find("Amount = '" + request.Liability + "' and Territory like '%" + request.Territory + "%'", "Amount");
					if (liabilityVarList1.Count > 0)
					{
						request.AddTPL = Convert.ToDecimal(liabilityVarList1[0].Value);
						decimal tmpbasicprem = (request.BasicPremium + (request.BasicPremium * response.RateUP / 100));
						response.AddTPL = tmpbasicprem * request.AddTPL;
						request.AddTPL = response.AddTPL;
					}
				}




				//tmpBasicPremium = request.BasicPremium;
				//request.GovTax = 0;
				List<Client> clientList = ClientRepository.Current.Find("ClientNo = '" + request.ClientNo + "'", "ClientNo");
				if (clientList.Count != 0)
				{
					if (clientList[0].TaxExcempt == false)// && sysList.Count != 0)
					{
						request.GovTax = sysList[0].Tax;
					}
					else
					{
						request.GovTax = 0;
					}
				}
				else
				{
					request.GovTax = sysList[0].Tax;
				}
				List<PremVar> premvarList = PremVarRepository.Current.Find("Territory = '" + request.Territory + "' and ID = '" + request.VehicleUse + "' and Coverage = '" + request.Coverage + "'", "Territory");
				if (premvarList.Count == 0)
				{
					//set default values to 0
					premvarList = new List<PremVar>();
					premvarList.Add(new PremVar()
					{
						Aald = 0.0,
						Actofgod = 0,
						Adddriver = 0,
						AgentStaffdisc = 0,
						Charge = 0,
						Fleetdisc = 0,
						Forlicense = 0,
						Licexp = 0,
						Passliab = 0,
						Staffdisc = 0,
						Underageperc = 0,
						Windscreen = 0
					}
					);

				}
				List<Vtype> vTypes = VtypeRepository.Current.Find("Vtype = '" + request.VehicleType + "' and CountryCode like '%" + request.Territory + "%'", "CountryCode");

				if (vTypes != null)
				{
					if (vTypes.Count > 0)
					{
						int todayYear = System.DateTime.Now.Year;
						int yearDiff = todayYear - request.VehicleYear;
						if (yearDiff >= vTypes[0].Rateupyear)
						{
							if (request.NoOverride != true)
							{
								if (vTypes[0].Rateupperc > 0) request.RateUp = vTypes[0].Rateupperc;
							}
						}

					}
				}
				//response.RateUpAmt = Math.Round(request.BasicPremium * System.Convert.ToDecimal(request.RateUp / 100), 2, MidpointRounding.AwayFromZero);
				//response.RateUP = System.Convert.ToDecimal(request.RateUp);
				
				tmpBasicPremium = Math.Round(request.BasicPremium + response.RateUpAmt, 2, MidpointRounding.AwayFromZero);
				response.BasicPremium = request.BasicPremium;


				if (request.Rental == false)
				{
					switch (request.VehicleUse)
					{
						case "PR":
							//if (request.DriverExp < 2) request.AALD = true;
							break;
						case "BS":
						case "SB":
						case "CP":
						case "HD":
						case "TX":
						case "RN":
							if (request.Territory == "SMD" || request.Territory == "SMF") request.AALD = true; //If vehicle use is commercial then AALD is automatically set to true
							break;
					}
					if (request.Territory == "SAB" && (request.AgeCat == 0 || request.DriverExp < 2)) // under 25 or less than 2 yrs driving experience
					{
						response.LicenseExp = Math.Round(tmpBasicPremium * System.Convert.ToDecimal(premvarList[0].Underageperc / 100), 2, MidpointRounding.AwayFromZero);
						tmpBasicPremium = Math.Round(tmpBasicPremium + response.LicenseExp, 2, MidpointRounding.AwayFromZero);
					}
					if ((request.Territory == "EUX" || request.Territory == "MON") && request.AALD)
					{
						response.AALD = tmpBasicPremium * System.Convert.ToDecimal(premvarList[0].Aald / 100);
						tmpBasicPremium = Math.Round(tmpBasicPremium + response.AALD, 2, MidpointRounding.AwayFromZero);
					}
					//Added on 3 Jan 2014 on Request
					if (request.AALD && (request.Territory == "ARU" || request.Territory == "CUR" || request.Territory == "BON"))
					{
						response.AALD = tmpBasicPremium * System.Convert.ToDecimal(premvarList[0].Aald / 100);
						tmpBasicPremium = Math.Round(tmpBasicPremium + response.AALD, 2, MidpointRounding.AwayFromZero);
					}
					//----------------------------------
					if (request.AgeCat == 0 && request.Territory == "EUX")
					{
						response.LicenseExp = Math.Round(response.BasicPremium * System.Convert.ToDecimal(premvarList[0].Underageperc / 100), 2, MidpointRounding.AwayFromZero);
						tmpBasicPremium = Math.Round(tmpBasicPremium + response.LicenseExp, 2, MidpointRounding.AwayFromZero);
					}
					if (request.AALD && request.DriverExp > 2 && request.Territory == "SAB")
					{
						response.AALD = Math.Round(tmpBasicPremium * System.Convert.ToDecimal(premvarList[0].Aald / 100), 2, MidpointRounding.AwayFromZero);
						tmpBasicPremium = Math.Round(tmpBasicPremium + response.AALD, 2, MidpointRounding.AwayFromZero);
					}
					else
					{
						if (request.DriverExp < 2 && (request.Territory == "MON")) // || request.Territory == "SAB"
						{
							response.LicenseExp = Math.Round(tmpBasicPremium * System.Convert.ToDecimal(premvarList[0].Licexp / 100), 2, MidpointRounding.AwayFromZero);
							tmpBasicPremium = Math.Round(tmpBasicPremium + response.LicenseExp, 2, MidpointRounding.AwayFromZero);
						}
					}

					if (request.ForLicense)
					{
						response.ForLicense = Math.Round(tmpBasicPremium * System.Convert.ToDecimal(premvarList[0].Forlicense / 100), 2, MidpointRounding.AwayFromZero);
						tmpBasicPremium = Math.Round(tmpBasicPremium + response.ForLicense, 2, MidpointRounding.AwayFromZero);
					}
				}
				GrsAmt = tmpBasicPremium;

				//'------------------------------------------------------------
				//''In the event that function is called from CalcPremium
				//'-----------------------------------------------------------
				if (testOveride)
				{
					List<LiabilityVar> liabilityVarList = LiabilityVarRepository.Current.Find("Amount = '" + request.Liability + "' and Territory like '%" + request.Territory + "%'", "Amount");

					if (liabilityVarList.Count > 0)
					{
						if (request.Liability == "Naf 200,000.00" && request.NewVehDiscount && (request.Territory == "SMD" || request.Territory == "SMF"))
						{
							request.AddTPL = 0;
						}
						if (request.Liability == "US$ 100,000.00" && request.NewVehDiscount && (request.Territory == "SAB" || request.Territory == "EUX"))
						{
							request.AddTPL = 0;
						}
						else
						{
							if (request.Territory != "CUR" && request.Territory != "BON" && request.Territory != "ARU")
							{
								request.AddTPL = Convert.ToDecimal(liabilityVarList[0].Value);
							}
						}
					}
				}


				if (request.Territory == "ARU" || request.Territory == "CUR" || request.Territory == "BON")
				{
					response.AddTPL = request.AddTPL;
					GrsAmt = GrsAmt + request.AddTPL + request.ToolsOfTrade;
				}
				else
				{
					response.AddTPL = request.AddTPL;
					GrsAmt = GrsAmt + request.AddTPL + request.ToolsOfTrade;
				}

				if (request.FleetDiscount)
				{
					response.Fleetdisc = GrsAmt * (Convert.ToDecimal(premvarList[0].Fleetdisc) / 100);
					GrsAmt = GrsAmt - response.Fleetdisc;
				}


				if (request.StaffDiscount)
				{
					if (request.MasterAgentCode == "NON" || request.MasterAgentCode == "EMP") // Direct NAGICO Staff
					{
						response.StaffDisc = GrsAmt * (Convert.ToDecimal(premvarList[0].Staffdisc) / 100);
					}
                    else
                    {
						response.StaffDisc = GrsAmt * (Convert.ToDecimal(20) / 100);
					}
					GrsAmt = GrsAmt - response.StaffDisc;
				}

				//List<NVehUse> nVehUseList = NVehUseRepository.Current.Find("VuseID='" + request.VehicleUse + "'", "VuseID");

				if (premvarList.Count > 0) response.PassLiab = request.PassLiab * premvarList[0].Passliab; // nVehUseList[0].Passliab;

				if (request.ManagDisc > 0)
				{
					response.ManagDisc = GrsAmt * Convert.ToDecimal(request.ManagDisc) / 100;
					GrsAmt = GrsAmt - response.ManagDisc;
				}

				response.YearPremium = GrsAmt;
				 if (request.Territory == "BAH" ) response.YearPremium = request.Grosspremium;
				response.GrossPremium = response.YearPremium;

				//if (request.Territory == "ARU") response.GrossPremium = response.GrossPremium * 1.02m; //add 2% tax to the gross premium


				if (request.Period < 12 & request.ShortPeriod == false)
				{
					response.GrossPremium = response.YearPremium * (Convert.ToDecimal(request.RateCode) / 100);

					switch (request.PremiumRateCode)
					{
						case "1Q":
							response.GrossPremium = response.YearPremium * (Convert.ToDecimal(premvarList[0].Quarterlyperc) / 100);
							break;
						case "3M50":
						case "9M50":
							response.GrossPremium = response.YearPremium / 2;
							break;
						case "6M":
							response.GrossPremium = response.YearPremium * (Convert.ToDecimal(premvarList[0].Halfyearlyperc) / 100);
							break;
						case "3M":
							response.GrossPremium = Math.Round(response.YearPremium * System.Convert.ToDecimal(0.4), 2, MidpointRounding.AwayFromZero);
							break;
						case "9M":
							response.GrossPremium = Math.Round(response.YearPremium * System.Convert.ToDecimal(0.4), 2, MidpointRounding.AwayFromZero);
							break;
						case "6M1":
							response.GrossPremium = Math.Round(response.YearPremium * System.Convert.ToDecimal(0.7), 2, MidpointRounding.AwayFromZero);
							break;
						case "6M2":
							response.GrossPremium = Math.Round(response.YearPremium * System.Convert.ToDecimal(0.3), 2, MidpointRounding.AwayFromZero);
							break;
					}
				}
				response.GrossPremium = Math.Round(response.GrossPremium, 2, MidpointRounding.AwayFromZero);

				if (request.ShortPeriod)
				{
					//List<ShortPeriod> shortPeriodList = ShortPeriodRepository.Current.Find("Description = '" + request.ShortPeriodText + "'", "Description");
					//response.GrossPremium = response.YearPremium * (Convert.ToDecimal(shortPeriodList[0].Value / 100));
					response.GrossPremium = (response.YearPremium * request.ShortPeriod_Perc) / 100;
				}

				//For Aruba, the policies are getting 1.5% Tax in the grospremium calculated as of jan 2023
				//if (request.Territory == "ARU" && request.Coverage != "T")
				//{
				//	response.GrossPremium = response.GrossPremium * 1.015m;
				//}



				if (request.NCD != 0) response.NCD = response.GrossPremium * Convert.ToDecimal(request.NCD / 100);

				response.DiscountPremium = response.GrossPremium - response.NCD;

				// Changed by Vincent 16 Oct 2012
				// Nagico gives an extra 30% discount on New Vehicles
				// and Liability of Naf 200,000.00 without extra charge if the NCD is maxium NCD
				// Total Discount can't be more than 60% (Maximum dicount for that coverage

				//Check first what the Max NCD for this coverage is
				List<Coverage> covlist = null;
				PremVar premVar = null;
				int maxncd = 0;
				string coverage = request.Coverage;
				covlist = CoverageController.Find("CoverageCode = '" + request.Coverage + "' AND Territory like '%" + request.Territory + "%'", "CoverageCode");
				List<PremVar> premV = PremVarRepository.Current.GetByIDCoverageTerritory(request.VehicleUse, request.Coverage, request.Territory);
				if (premV.Count > 0)
				{
					premVar = premV[0];
				}
				else
				{
					string msg = "The following fields for " + request.PolicyNo + " are empty. Calculation for this policy will not continue untill fixed \r\n\r\n";
					if (request.VehicleUse == string.Empty) msg += " vehile use \r\n";
					if (request.Coverage == string.Empty) msg += " coverage \r\n";
					MessageBox.Show(msg);
				}


				if (covlist.Count == 0)
				{
					//set default values to 0
					covlist = new List<Coverage>();
					covlist.Add(new Coverage()
					{
						Bonusmalus = 0,
						Loadperc = 0,
						Maxncd = 0

					}
					);

				}
				if (covlist != null)
				{

					//changes made for Saba and Statia upon request from Bharo's email dated @ 1-FEB-2013:
					//quote: "The maximum discount in all other cases not described above will be capped at 70% for Comprehensive and 50% for Third Party."
					//----BEGIN-------------
					if (request.Territory == "SAB" || request.Territory == "EUX")
					{
						maxncd = covlist[0].Bonusmalus; //use another value stored in the table
					}
					else
					{
						maxncd = covlist[0].Maxncd;
					}
					//----END-------------
				}
				//added request.DontOverrideNVD == false in order to keep current values
				//added on 19-Nov-2012
				if (request.NewVehDiscount && request.DontOverrideNVD == false)
				{
					response.NewVehDiscValue = 0;
					response.NewVehDiscPerc = 0;
					//if the client has a fleet discount, than that also needs to be added to the total discount calculation
					if (request.FleetDiscount)
					{
						if (100 - (sysList[0].Newvehdisc * 100) + Convert.ToDecimal(request.NCD) + Convert.ToDecimal(premVar.Fleetdisc) <= maxncd)
						{
							response.NewVehDiscPerc = 100 - (sysList[0].Newvehdisc * 100);
							response.NewVehDiscValue = response.DiscountPremium - (Math.Round(response.DiscountPremium * sysList[0].Newvehdisc, 2, MidpointRounding.AwayFromZero));
						}
					}
					else
					{
						if (100 - (sysList[0].Newvehdisc * 100) + Convert.ToDecimal(request.NCD) <= maxncd)
						{
							response.NewVehDiscPerc = 100 - (sysList[0].Newvehdisc * 100);
							response.NewVehDiscValue = response.DiscountPremium - (Math.Round(response.DiscountPremium * sysList[0].Newvehdisc, 2, MidpointRounding.AwayFromZero));
						}
					}
				}
				// Added by Vincent on Mar 28, 2013
				//if override is checked, use the value that is entered

				if (request.DontOverrideNVD)
				{
					response.NewVehDiscPerc = request.NVDPerc;
					response.NewVehDiscValue = (Math.Round((response.DiscountPremium * (request.NVDPerc / 100)), 2, MidpointRounding.AwayFromZero));
				}

				//_______________________________________________________________
				// Added by Vincent on May 23, 2014
				//if the discounted premium is less then the absolute min premium, use minmum premium
				if (request.Territory == "BAH")
				{
					if (request.NoOverride == false)
					{
						// Changed by Vincent on November 28, 2023 upon request

						//response.DiscountPremium = request.DiscountPremium;
						//response.NCD = request.NCDValue;
						if (response.DiscountPremium < request.AbsoluteMinPrem) request.DiscountPremium = request.AbsoluteMinPrem;
					}
					else
					{
						response.DiscountPremium = request.BasicPremium;
					}
					response.GovTax = request.GovTax;

				}

                    if (request.PassLiab > 0)
				{
					response.PassLiab = request.PassLiab * premvarList[0].Passliab;
					switch (request.PremiumRateCode)
					{
						case "1Q":
							response.PassLiab = response.PassLiab * (Convert.ToDecimal(premvarList[0].Quarterlyperc) / 100);
							break;
						case "3M50":
						case "9M50":
							response.PassLiab = response.PassLiab / 2;
							break;
						case "6M":
							if (request.Territory == "CUR" || request.Territory == "BON" || request.Territory == "ARU")// || request.Territory == "BON")
							{
								response.PassLiab = response.PassLiab / 2;
							}
							else
							{
								response.PassLiab = response.PassLiab * (Convert.ToDecimal(premvarList[0].Halfyearlyperc) / 100);
							}
							break;
						case "3M":
							response.PassLiab = Math.Round(response.PassLiab * System.Convert.ToDecimal(0.4), 2, MidpointRounding.AwayFromZero);
							break;
						case "9M":
							response.PassLiab = Math.Round(response.PassLiab * System.Convert.ToDecimal(0.4), 2, MidpointRounding.AwayFromZero);
							break;
						case "6M1":
							response.PassLiab = Math.Round(response.PassLiab * System.Convert.ToDecimal(0.7), 2, MidpointRounding.AwayFromZero);
							break;
						case "6M2":
							response.PassLiab = Math.Round(response.PassLiab * System.Convert.ToDecimal(0.3), 2, MidpointRounding.AwayFromZero);
							break;
					}
				}

				if (request.isPercentageCampaign) response.CampaignAmt = ((response.DiscountPremium - response.NewVehDiscValue + response.PassLiab + request.ExtraCoverage1 + request.ExtraCoverage2) * request.Campaigns) / 100; else response.CampaignAmt = request.Campaigns;
				decimal BahEndorsements = 0m;
				//if (request.Territory == "BAH")
				//if (request.ForLicense == true || request.Rental == true)
				//{
				//    BahEndorsements = 97.06m;
				//}

				response.NetPremium = response.DiscountPremium - response.NewVehDiscValue + response.PassLiab + request.ExtraCoverage1 + request.ExtraCoverage2 + response.CampaignAmt + BahEndorsements;
				//see if the NetPremium is more than the minimal premium

				//For Aruba for now, hardcode 2% TOT that can't be calculated in the tax field
				//-Added on 6-Jan-2022
				//DateTime dt = (DateTime)request.EffectiveDate;
				//if (request.Territory == "ARU" && (request.Coverage != "TP" || dt.Year >= 2023))  // No Tax for TP vehiocles in Aruba
				//{
				//	response.NetPremium = response.NetPremium * 1.02m; //add 2% TOT
				//}


				if (request.Territory == "BAH" && request.NoOverride == true)
				{

				}
				else
				{
					List<MinimumPremium> minprem = MinimumPremiumController.Find("PolTyp= '" + request.PolType + "' and string1 = '" + request.Coverage + "' and string2 like '%" + request.VehicleUse + "%'", "PolTyp");
					if (minprem != null)
					{
						if (minprem.Count > 0)
						{
							switch (request.PremiumRateCode)
							{
								case "1Q":
									if ((response.NetPremium) < minprem[0].Premium / 4)
									{
										response.NetPremium = minprem[0].Premium / 4;
										response.Message = "Minimum premium was applied";
									}
									break;
								case "3M50":
								case "9M50":
								case "6M":
									if ((response.NetPremium) < minprem[0].Premium / 2)
									{
										response.NetPremium = minprem[0].Premium / 2;
										response.Message = "Minimum premium was applied";
									}
									break;
								case "1Y":
									if ((response.NetPremium) < minprem[0].Premium)
									{
										response.NetPremium = minprem[0].Premium;
										response.Message = "Minimum premium was applied";
									}
									break;
							}
						}
					}
				}
                if (request.Territory == "BAH" )
                {
                    if (request.NoOverride == true)
                    {

                        //response.NetPremium = request.BasicPremium;
                        //response.Message = string.Empty;
                    }
                }
                response.PolicyFee = request.Policyfee;

				if (request.GovTax != 0)
				{
					if (request.Territory == "SLU")
					{
						response.GovTax = response.NetPremium * request.GovTax;
					}
					else if (request.Territory == "BAH")
					{
						response.GovTax = (response.NetPremium + response.PolicyFee) * request.GovTax;
						response.GovTax = Math.Round((response.GovTax / 0.05m), 0) * 0.05m;
						response.GovVAT = (response.NetPremium + response.PolicyFee + response.GovTax) * request.GovVAT;
						response.GovVAT = Math.Round((response.GovVAT / 0.05m), 0) * 0.05m;
					}
					else
					{
						response.GovTax = (response.NetPremium + response.PolicyFee) * request.GovTax;
						response.GovVAT = (response.NetPremium + response.PolicyFee + response.GovTax) * request.GovVAT;
					}

				}

				//exclude govTax for TP vehicles Aruba; Add RSS_Fee to PolicyFee
				if (request.Territory == "ARU" && (request.Coverage == "TP" || request.EffectiveDate.Year < 2023))
				{
					response.GovTax = 0;

				}
				if (request.Territory == "ARU")
				{
					premVar = PremVarRepository.Current.GetByIDCoverageTerritory(request.VehicleUse, coverage, request.Territory)[0];
					//for new business only after 1 Feb 2024
					if (request.EffectiveDate.Year >= 2024) response.RSS_Fee = premVar.RSS_Fee;

					//see if RSSfee needs to be charged or not
					if (request.NoRSSFeeCharge == true)
					{
						response.RSS_Fee = 0m;
						request.NRSA = 0m;


					}
				}
				if (request.Territory == "SAB" && (request.EffectiveDate.Year < 2024))
				{
					response.GovTax = 0;
				}
				if (request.Territory == "SAB" || request.Territory == "EUX")
				{
					DateTime dreffdate = Convert.ToDateTime(request.EffectiveDate);
					if (dreffdate.Year < 2024)
					{
						response.GovTax = 0.0m;
					}
					else if (dreffdate.Year >= 2024 || dreffdate.Month >= 2)
					{
						response.GovTax = Math.Round((response.NetPremium + response.PolicyFee) * 0.05M, 2);
					}
				}
				response.BasicPremium = request.BasicPremium;
				response.NetPremium = Math.Round(response.NetPremium, 2, MidpointRounding.AwayFromZero);
				response.GovTax = Math.Round(response.GovTax, 2, MidpointRounding.AwayFromZero);
				response.GovVAT = Math.Round(response.GovVAT, 2, MidpointRounding.AwayFromZero);
				response.NRSA = request.NRSA;
				response.TotalPremium = response.NetPremium + response.PolicyFee + response.NRSA + response.GovTax + response.GovVAT + BahEndorsements + response.RSS_Fee;

				//New request for EC$ Territories, round to the nearest $0.05 as per 1 July 2015)
				if (request.Territory == "GRE" || request.Territory == "SKT" || request.Territory == "NEV" || request.Territory == "MON" || request.Territory == "SLU" || request.Territory == "DOM" || request.Territory == "ANG")
				{
					response.NetPremium = Math.Round((response.NetPremium / 0.05m), 0) * 0.05m;
					response.PolicyFee = Math.Round((response.PolicyFee / 0.05m), 0) * 0.05m;
					response.GovTax = Math.Round((response.GovTax / 0.05m), 0) * 0.05m;
					response.GovVAT = Math.Round((response.GovVAT / 0.05m), 0) * 0.05m;
					response.TotalPremium = Math.Round((response.TotalPremium / 0.05m), 0) * 0.05m;
				}
			}
			catch (Exception exception)
			{
				ErrorLogAdapter.Current.LogError(exception);
			}
			return response;
		}

        #endregion "No CalcNoOverrideOverride"

        #endregion Premium Calculation

        #region Custom Funcions
        public void CalcuatePremium(ref Policy _policy)
		{
			switch (_policy.GetType().Name)
			{
				case "PolicyLE":
					_policy = calculatePremiumLE((PolicyLE)_policy);
					break;
				
				default:
					break;
			}
		}
		private DataSet Getdataset()
		{
			try
			{
				string countrycode = req.Territory;
				string poltype = req.PolType;
				string coverage = req.Coverage;
				DateTime effDate = req.EffectiveDate;
				DateTime appDate = req.ApplicationDate;
				string trxcode = string.Empty;
				if (req.IsNew == true)
				{
					trxcode = "NEW";
				}
				else
				{
					trxcode = "REN";
				}
				string connString = InsproConfiguration.GetSettings().ConnectionString;
				string sql = @"Select Rate from [CompRateUpdates]" +
							" WHERE CountryCode= '" + countrycode + "' AND PolType='" + poltype + "'" +
							" AND CoverageCode='" + coverage + "' AND Date_Effective <= '" + effDate + "'";
				sql += " AND TrxCode = '" + trxcode + "'";
				using (SqlConnection con = new SqlConnection(connString))
				using (SqlCommand cmd = new SqlCommand())
				using (SqlDataAdapter da = new SqlDataAdapter())
				{
					DataSet ds = new DataSet();
					con.Open();
					cmd.Connection = con;
					cmd.CommandText = sql;
					da.SelectCommand = cmd;
					da.Fill(ds, "CompRateUpdates");
					// Check if any records were returned
					if (ds.Tables.Count > 0 && ds.Tables["CompRateUpdates"].Rows.Count > 0)
					{
						return ds; // Records were returned
					}
					else
					{
						return null; // No records were returned
					}
				}
			}
			catch (Exception ex)
			{
				return null;
			}
		}
		public PremiumResponse GetNewGrossPremium(Inspro.VwAgentrenewal var)
		{
			PremiumRequest request = new PremiumRequest();
			PremiumResponse response = new PremiumResponse();
			decimal grosspremium = 0m;
			string engine = string.Empty;
			List<Inspro.Vehicle> vehicles = VehicleController.GetByID(var.Vehid);
			Vehicle veh = vehicles[0];
			List<EngineSize> vEngine = EngineSizeController.Find("Description = '" + veh.Enginesize + "'", "Value");

			request.PolicyNo = var.Policyno;
			request.ClientNo = var.Clientno;
			request.AgentCode = var.Agentcode;
			request.MasterAgentCode = var.Masteragentcode;
			request.PolType = var.Poltype;
			request.EffectiveDate = var.DateEffective;
			request.NCDDate = var.Ncddate;
			request.Coverage = var.Coveragecode;
			request.Territory = var.Countrycode;
			request.CurrCode = var.Currcode;
			request.NoOverride = veh.Bit1;
			request.VehicleValue = veh.Vehiclevalue;
			request.VehicleUse = veh.Vehuse;
			request.PremiumRateCode = var.Ratecode;
			request.Period = Convert.ToInt16(veh.Period);
			request.LicensePlateNo = var.Licplateno;
			if (var.Ratecode == "1Y") request.Period = 12;
			if (vEngine != null && vEngine.Count>0)
			{
				if (request.Territory != "BAH") request.EngineSize = Convert.ToInt32(vEngine[0].Value);
			}
			
			if (veh.InsuredSex == "0") request.InsuredSex = Gender.M; else request.InsuredSex = Gender.F;
			
			switch (veh.InsuredAge)
			{
				case "30 +":
					request.AgeCat = 31;
					break;

				case "25 +":
					request.AgeCat = 26;
					break;

				case "30 -":
					request.AgeCat = 0;
					break;

				default:
					if (request.Territory == "BON")
					{
						request.AgeCat = 31;
					}
					else
					{
						request.AgeCat = Convert.ToInt16(veh.InsuredAge);
					}
					break;
			}

			switch (veh.Licenseexp.ToLower())
			{
				case "more than 2 years":
					request.DriverExp = 3;
					break;

				case "less than 2 years":
				case "less than 6 months":
					request.DriverExp = 0;
					break;

				default:
					request.DriverExp = 3;
					break;
			}

			request.NCD = veh.NcdPerc;
			request.BasicPremium = var.Basicpremium;
			if (veh.Fleetdisc > 0) request.FleetDiscount = true;
			request.RateUp = veh.Rateup;
			request.ForLicense = veh.Foreignlicense;
			if (veh.Aald > 0) request.AALD = true;
			request.NewVehDiscount = veh.NewVehDisc;
			request.AddTPL = veh.Addtpl;
			request.ToolsOfTrade = veh.Toolsoftrade;
			if (veh.Fleetdisc > 0) request.FleetDiscount = true;
			if (veh.Staffdisc > 0) request.StaffDiscount = true;
			request.ManagDisc = Convert.ToDouble(veh.Mandisc);
			request.PassLiab = veh.Passseat;
			request.Liability = veh.Liability;
			request.PolicyNo = veh.Policyno;
			request.VehicleType = veh.Vehtype;
			request.LicensePlateNo = veh.Licplateno;
			
			//send the request to fetch the updated fields
			response = GetPremiumRate(request);

			if (response != null)
			{
				grosspremium += response.YearPremium;
			}
            else
            {
				MessageBox.Show(veh.Policyno + " with Lic. Plate: " + veh.Licplateno + " has an error calclating GrossPremium", "GetNewGrossPremium - error");
            }
			//update vehicle with new calculated data
			veh.Basicpremium = response.BasicPremium;
			veh.Licenseexp = response.LicenseExp.ToString();
			veh.Aald = response.AALD;
			veh.Fleetdisc = response.Fleetdisc;
			veh.Staffdisc = response.StaffDisc;
			veh.Mandisc = response.ManagDisc;
			veh.Yearpremium = response.YearPremium;
			veh.Grosspremium = response.GrossPremium;
			veh.Ncd = response.NCD;
			veh.Nettpremium = response.NetPremium;
			if(var.Ratecode == "1Y") response.Period = 12;
			if (var.Ratecode == "9M50") response.Period = 9;
			if (var.Ratecode == "6M") response.Period = 6;
			if (var.Ratecode == "1Q") response.Period = 3;
			//

			return response;
		}
		#endregion Custom Funcions
	}
}

