using System.Text.Json;
using System.Text.Json.Nodes;
using CrashReport.Data;
using CrashReport.Models;
using CrashReport.Models.Dtos;
using CrashReport.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace CrashReport.Services;

public class CrashCaptureService : ICrashCaptureService
{
    private readonly AppDbContext _context;
    private readonly ICrashFormValidationService _validation;
    private readonly IWebHostEnvironment _env;

    public CrashCaptureService(AppDbContext context, ICrashFormValidationService validation, IWebHostEnvironment env)
    {
        _context = context;
        _validation = validation;
        _env = env;
    }

    public async Task<CrashSubmitResult> SubmitAsync(string formJson)
    {
        if (string.IsNullOrEmpty(formJson))
        {
            return new CrashSubmitResult { Outcome = CrashSubmitOutcome.EmptyForm };
        }

        
        var formNode = JsonNode.Parse(formJson);
        try
        {
            var info = formNode?["CrashInfo"] ?? throw new ArgumentException("Accident details are required.");
            info["CrNo"] = CrashNumberFormatter.Format(info["SapsStation"]?.GetValue<string>() ?? "",
                info["CrNo"]?.GetValue<string>() ?? "");
            formJson = formNode!.ToJsonString();
        }
        catch (ArgumentException ex)
        {
            return new CrashSubmitResult
            {
                Outcome = CrashSubmitOutcome.ValidationFailed,
                ValidationErrors = new List<string> { ex.Message },
                FormJsonForRedisplay = formJson
            };
        }

        var vmForValidation = JsonSerializer.Deserialize<CrashReportFormViewModel>(
            formJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();

        var validationErrors = await _validation.ValidateAsync(vmForValidation, existingCrashId: null);
       
        using (var duplicateDocument = JsonDocument.Parse(formJson))
        {
            if (duplicateDocument.RootElement.TryGetProperty("CrashInfo", out var info))
            {
                var number = GetString(info, "CrNo");
                if (!string.IsNullOrWhiteSpace(number) &&
                    await _context.Crashes.AnyAsync(c => c.CrNo == number))
                    validationErrors.Add($"A full accident report with AR/CR number '{number}' already exists.");
            }
        }
        if (validationErrors.Count > 0)
        {
            return new CrashSubmitResult
            {
                Outcome = CrashSubmitOutcome.ValidationFailed,
                ValidationErrors = validationErrors,
                FormJsonForRedisplay = formJson
            };
        }

        // Start a transaction to guarantee all-or-nothing
        await using var transaction = await _context.Database.BeginTransactionAsync();

        
        var existsAsSummary = false;

        try
        {
            using var document = JsonDocument.Parse(formJson);
            var root = document.RootElement;

            var crash = new Crash();
            _context.Crashes.Add(crash);

           
            if (root.TryGetProperty("CrashInfo", out var crashInfo))
            {
                crash.CasNo = GetString(crashInfo, "CasNo");
                crash.CrNo = GetString(crashInfo, "CrNo");
                crash.CapturingNumber = GetString(crashInfo, "CapturingNumber");
                crash.IncidentReportNo = GetString(crashInfo, "IncidentReportNo");
                crash.RoadNumber = GetString(crashInfo, "RoadNumber");
                crash.KmMarker = GetString(crashInfo, "KmMarker");
                crash.BriefDescription = GetString(crashInfo, "BriefDescription");
                crash.ProvinceCode = GetString(crashInfo, "ProvinceCode");
                crash.SpeedLimitKmh = GetShort(crashInfo, "SpeedLimitKmh");
                crash.NoOfAppendices = GetByte(crashInfo, "NoOfAppendices", 0);

                if (crashInfo.TryGetProperty("CrashDate", out var dateEl) && dateEl.ValueKind == JsonValueKind.String)
                    crash.CrashDate = DateOnly.TryParse(dateEl.GetString(), out var d) ? d : DateOnly.FromDateTime(DateTime.Today);

                if (crashInfo.TryGetProperty("CrashTime", out var timeEl) && timeEl.ValueKind == JsonValueKind.String)
                    crash.CrashTime = TimeOnly.TryParse(timeEl.GetString(), out var t) ? t : null;

               
                crash.VehicleString = GetString(crashInfo, "VehicleString");

                
                if (!string.IsNullOrWhiteSpace(crash.CrNo))
                    existsAsSummary = await _context.CrashSummaries.AnyAsync(s => s.CrNo == crash.CrNo);
            }

            
            if (root.TryGetProperty("Location", out var location))
            {
                var crashLocation = new CrashLocation
                {
                    Crash = crash,
                    BuiltUpArea = GetBool(location, "BuiltUpArea"),
                    AreaType = GetString(location, "AreaType"),
                    StreetRoadName = GetString(location, "StreetRoadName"),
                    GpsXCoordinate = GetDecimal(location, "GpsXCoordinate"),
                    GpsYCoordinate = GetDecimal(location, "GpsYCoordinate"),
                    IntersectionStreet = GetString(location, "IntersectionStreet"),
                    IntersectionRoadNo = GetString(location, "IntersectionRoadNo"),
                    BetweenFrom = GetString(location, "BetweenFrom"),
                    BetweenTo = GetString(location, "BetweenTo"),
                    Suburb = GetString(location, "Suburb"),
                    CityTown = GetString(location, "CityTown"),
                    DistanceKm = GetDecimal(location, "DistanceKm"),
                    CompassDirection = GetString(location, "CompassDirection"),
                    FromPoint = GetString(location, "FromPoint"),
                    KmMarkerInfo = GetString(location, "KmMarkerInfo"),
                    NextCityTown = GetString(location, "NextCityTown"),
                    RoadFunctionalClassification = GetString(location, "RoadFunctionalClassification"),
                    JunctionType = GetString(location, "JunctionType"),
                    RoadLayout = GetString(location, "RoadLayout"),
                    RoadSurfaceType = GetString(location, "RoadSurfaceType"),
                    RoadSurfaceQuality = GetString(location, "RoadSurfaceQuality"),
                    RoadSurfaceCondition = GetString(location, "RoadSurfaceCondition")
                };
                _context.CrashLocations.Add(crashLocation);
            }

            
            if (root.TryGetProperty("Conditions", out var conditions))
            {
                var crashCondition = new CrashCondition
                {
                    Crash = crash,
                    LightCondition = GetString(conditions, "LightCondition"),
                    TrafficControlType = GetString(conditions, "TrafficControlType"),
                    CrashType = GetString(conditions, "CrashType"),
                    HitAndRun = GetBool(conditions, "HitAndRun"),
                    RoadSegmentGrade = GetString(conditions, "RoadSegmentGrade"),
                    ObstructionType = GetString(conditions, "ObstructionType"),
                    RoadSignsCondition = GetString(conditions, "RoadSignsCondition"),
                    RoadMarkingVisibility = GetString(conditions, "RoadMarkingVisibility"),
                    OvertakingControl = GetString(conditions, "OvertakingControl"),
                    TyreBurstObserved = GetString(conditions, "TyreBurstObserved"),
                    VehicleLightsCondition = GetString(conditions, "VehicleLightsCondition"),
                    OtherObservations = GetString(conditions, "OtherObservations"),
                };
                _context.CrashConditions.Add(crashCondition);

                // Weather
                if (conditions.TryGetProperty("WeatherConditions", out var weatherEl) && weatherEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var w in weatherEl.EnumerateArray())
                    {
                        if (w.ValueKind == JsonValueKind.String)
                        {
                            _context.CrashWeathers.Add(new CrashWeather
                            {
                                Crash = crash,
                                WeatherCondition = w.GetString()
                            });
                        }
                    }
                }
            }

            // ========== 4. VEHICLES ==========
            var vehicleMakes = new List<string>();
            if (root.TryGetProperty("Vehicles", out var vehicles) && vehicles.ValueKind == JsonValueKind.Array)
            {
                foreach (var ve in vehicles.EnumerateArray())
                {
                    // ---- 4a. Vehicle (static) ----
                    var vehicle = new Vehicle
                    {
                        CountryOfRegistration = GetString(ve, "CountryOfRegistration") ?? "ZA",
                        LicenceDiscNumber = GetString(ve, "LicenceDiscNumber"),
                        Colour = GetString(ve, "Colour"),
                        Make = GetString(ve, "Make"),
                        Model = GetString(ve, "Model"),
                        VinNumber = GetString(ve, "VinNumber"),
                        VehicleType = GetString(ve, "VehicleType"),
                        TrailerLicenceNumber = GetString(ve, "TrailerLicenceNumber"),
                        VehicleCategory = GetString(ve, "VehicleCategory"),
                        VehicleTypeCode = GetString(ve, "VehicleTypeCode"),
                        SpecialFunction = GetString(ve, "SpecialFunction"),
                        PrivateOrBusiness = GetString(ve, "PrivateOrBusiness"),
                        LicenceTypeFitting = GetString(ve, "LicenceTypeFitting"),
                        CreatedAt = DateTime.Now
                    };
                    _context.Vehicles.Add(vehicle);
                    await _context.SaveChangesAsync();

                   
                    if (!string.IsNullOrEmpty(vehicle.Make))
                        vehicleMakes.Add(vehicle.Make);

                   
                    var crashVehicle = new CrashVehicle
                    {
                        Crash = crash,
                        Vehicle = vehicle,
                        VehicleReference = GetString(ve, "VehicleReference"),
                        VehicleManoeuvre = GetString(ve, "VehicleManoeuvre"),
                        SeatbeltUsed = GetString(ve, "SeatbeltHelmetUsed"),  
                        AlcoholSuspected = GetString(ve, "AlcoholSuspected"),
                        AlcoholTestResult = GetString(ve, "AlcoholTestResult"),
                        DrugSuspected = GetString(ve, "DrugSuspected"),
                        DrugTestResult = GetString(ve, "DrugTestResult"),
                        PositionBeforeCrash = GetString(ve, "PositionBeforeCrash"),
                        VehicleType = GetString(ve, "VehicleType"),
                        PassengersForReward = GetString(ve, "PassengersForReward"),
                        BreakdownCompany = GetString(ve, "BreakdownCompany"),

                    };
                    _context.CrashVehicles.Add(crashVehicle);
                    await _context.SaveChangesAsync(); 

                    
                    if (ve.TryGetProperty("VehicleDamages", out var damages) && damages.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var damage in damages.EnumerateArray())
                        {
                            if (damage.ValueKind == JsonValueKind.String)
                            {
                                _context.VehicleDamages.Add(new VehicleDamage
                                {
                                    CrashVehicleId = crashVehicle.CrashVehicleId,
                                    DamagePoint = damage.GetString()!
                                });
                            }
                        }
                    }


                    if (!string.IsNullOrWhiteSpace(GetString(ve, "GoodsCarried")) ||
                        !string.IsNullOrWhiteSpace(GetString(ve, "UnNumber")) ||
                        !string.IsNullOrWhiteSpace(GetString(ve, "CompanyName")))
                    {
                        _context.DangerousGoods.Add(new DangerousGood
                        {
                            Crash = crash,
                            VehicleReference = crashVehicle.VehicleReference,
                            GoodsCarried = GetString(ve, "GoodsCarried"),
                            SpillageObserved = GetString(ve, "SpillageObserved"),
                            VapourGasEmission = GetString(ve, "VapourGasEmission"),
                            PlacardDisplayed = GetString(ve, "PlacardDisplayed"),
                            UnNumber = GetString(ve, "UnNumber"),
                            CompanyName = GetString(ve, "CompanyName"),
                            EmergencyServicesActivated = GetString(ve, "EmergencyServicesActivated")
                        });
                    }

                    
                    var driverSurname = GetString(ve, "DriverSurname");
                    if (!string.IsNullOrEmpty(driverSurname))
                    {
                        var driver = new Person
                        {
                            IdType = GetString(ve, "DriverIdType") ?? "RSA_ID",
                            IdNumber = GetString(ve, "DriverIdNumber"),
                            Age = GetByte(ve, "DriverAge"),
                            Surname = driverSurname,
                            FullNames = GetString(ve, "DriverFullNames") ?? "",
                            CountryOfOrigin = GetString(ve, "DriverCountryOfOrigin"),
                            Nationality = GetString(ve, "DriverNationality"),
                            PopulationGroup = GetString(ve, "DriverPopulationGroup"),
                            Gender = GetString(ve, "DriverGender"),
                            HomeAddress = GetString(ve, "DriverHomeAddress"),
                            CellPhone = GetString(ve, "DriverCellPhone"),
                            OtherPhone = GetString(ve, "DriverOtherPhone"),
                            WorkContactAddress = GetString(ve, "DriverWorkAddress"),
                            CreatedAt = DateTime.Now
                        };
                        _context.Persons.Add(driver);
                        await _context.SaveChangesAsync();

                        crashVehicle.DriverPersonId = driver.PersonId;


                        if (!string.IsNullOrEmpty(GetString(ve, "LicenceCode")))
                        {
                            _context.DriversLicences.Add(new DriversLicence
                            {
                                PersonId = driver.PersonId,
                                LicenceType = GetString(ve, "LicenceType"),
                                LicenceNumber = GetString(ve, "LicenceNumber"),
                                LicenceCode = GetString(ve, "LicenceCode"),
                                DateOfIssue = GetDateOnly(ve, "DateOfIssue"),
                                PrdpCode = GetString(ve, "PrdpCode")
                            });
                        }

                        
                        _context.CrashPeople.Add(new CrashPerson
                        {
                            Crash = crash,
                            Person = driver,
                            CrashVehicle = crashVehicle,
                            Role = "Driver",
                            VehicleReference = GetString(ve, "VehicleReference"),
                            SeverityOfInjury = GetString(ve, "SeverityOfInjury"),
                            PersonReference = GetString(ve, "PersonReference"),
                            PassengerNumber = GetByte(ve, "PassengerNumber"),
                            ChildRestraintUsed = GetString(ve, "ChildRestraintUsed"),
                            LiquorDrugSuspected = GetString(ve, "LiquorDrugSuspected"),
                            LiquorDrugTestDone = GetString(ve, "LiquorDrugTestDone"),
                            AmbulanceServiceRef = GetString(ve, "AmbulanceServiceRef"),
                            Hospital = GetString(ve, "Hospital"),

                        });
                    }

                    await _context.SaveChangesAsync();
                }
            }


            crash.NoOfVehiclesInvolved = (byte)(root.TryGetProperty("Vehicles", out var vArr) ? vArr.GetArrayLength() : 0);


            if (string.IsNullOrEmpty(crash.VehicleString) && vehicleMakes.Any())
                crash.VehicleString = string.Join(", ", vehicleMakes.Distinct());

            if (root.TryGetProperty("Persons", out var persons) && persons.ValueKind == JsonValueKind.Array)
            {
                foreach (var pe in persons.EnumerateArray())
                {
                    var surname = GetString(pe, "Surname");
                    if (string.IsNullOrEmpty(surname)) continue;

                    var person = new Person
                    {
                        IdType = GetString(pe, "DriverIdType") ?? "RSA_ID",
                        IdNumber = GetString(pe, "DriverIdNumber"),
                        Age = GetByte(pe, "DriverAge"),
                        Surname = surname,
                        FullNames = GetString(pe, "DriverFullNames") ?? "",
                        CountryOfOrigin = GetString(pe, "CountryOfOrigin"),
                        Nationality = GetString(pe, "Nationality"),
                        PopulationGroup = GetString(pe, "PopulationGroup"),
                        Gender = GetString(pe, "Gender"),
                        HomeAddress = GetString(pe, "HomeAddress"),
                        CellPhone = GetString(pe, "DriverCellPhone"),
                        OtherPhone = GetString(pe, "OtherPhone"),
                        WorkContactAddress = GetString(pe, "WorkContactAddress"),
                        CreatedAt = DateTime.Now
                    };
                    _context.Persons.Add(person);
                    await _context.SaveChangesAsync();

                    int? crashVehicleId = null;
                    var vehicleRef = GetString(pe, "VehicleReference");
                    if (!string.IsNullOrEmpty(vehicleRef))
                    {
                        var cv = await _context.CrashVehicles
                            .FirstOrDefaultAsync(cv => cv.CrashId == crash.CrashId &&
                                                       cv.VehicleReference == vehicleRef);
                        if (cv != null) crashVehicleId = cv.CrashVehicleId;
                    }

                    var crashPerson = new CrashPerson
                    {
                        Crash = crash,
                        Person = person,
                        CrashVehicleId = crashVehicleId,
                        Role = GetString(pe, "Role") ?? "Passenger",
                        VehicleReference = vehicleRef,
                        SeatingPosition = GetString(pe, "SeatingPosition"),
                        SeverityOfInjury = GetString(pe, "SeverityOfInjury"),
                        SeatbeltHelmetUsed = GetString(pe, "SeatbeltHelmet"),
                        Hospital = GetString(pe, "Hospital"),
                        PersonReference = GetString(pe, "PersonReference"),
                        PassengerNumber = GetByte(pe, "PassengerNumber"),
                        ChildRestraintUsed = GetString(pe, "ChildRestraint"),
                        LiquorDrugSuspected = GetString(pe, "LiquorDrugSuspected"),
                        LiquorDrugTestDone = GetString(pe, "LiquorDrugTestDone"),
                        AmbulanceServiceRef = GetString(pe, "AmbulanceReference"),
                    };

                    _context.CrashPeople.Add(crashPerson);
                    await _context.SaveChangesAsync();


                    var role = GetString(pe, "Role");
                    if (role == "Pedestrian" || role == "Bicyclist")
                    {
                        var detail = new PedestrianBicyclistDetail
                        {
                            CrashPersonId = crashPerson.CrashPersonId,
                            PositionOnRoad = GetString(pe, "PositionOnRoad"),
                            LocationReCrossing = GetString(pe, "LocationReCrossing"),
                            Manoeuvre = GetString(pe, "Manoeuvre"),
                            PedestrianAction = GetString(pe, "PedestrianAction"),
                            ClothingColour = GetString(pe, "ClothingColour")
                        };
                        _context.PedestrianBicyclistDetails.Add(detail);

                    }
                }
            }


            
            if (root.TryGetProperty("UninjuredPassengers", out var uninjured) && uninjured.ValueKind == JsonValueKind.Array)
            {
                foreach (var up in uninjured.EnumerateArray())
                {
                    var uSurname = GetString(up, "Surname");
                    if (string.IsNullOrEmpty(uSurname)) continue;

                    var uPerson = new Person
                    {
                        IdType = "RSA_ID",
                        IdNumber = GetString(up, "IdNumber"),
                        Surname = uSurname,
                        FullNames = "",
                        CellPhone = GetString(up, "CellPhone"),
                        CreatedAt = DateTime.Now
                    };
                    _context.Persons.Add(uPerson);
                    await _context.SaveChangesAsync();

                    int? uCrashVehicleId = null;
                    var uVehicleRef = GetString(up, "VehicleReference");
                    if (!string.IsNullOrEmpty(uVehicleRef))
                    {
                        var uCv = await _context.CrashVehicles
                            .FirstOrDefaultAsync(cv => cv.CrashId == crash.CrashId &&
                                                       cv.VehicleReference == uVehicleRef);
                        if (uCv != null) uCrashVehicleId = uCv.CrashVehicleId;
                    }

                    _context.CrashPeople.Add(new CrashPerson
                    {
                        Crash = crash,
                        Person = uPerson,
                        CrashVehicleId = uCrashVehicleId,
                        Role = "Passenger",
                        VehicleReference = uVehicleRef,
                        SeverityOfInjury = "No injury"
                    });
                    await _context.SaveChangesAsync();
                }
            }


            
            if (root.TryGetProperty("Factors", out var factors) && factors.ValueKind == JsonValueKind.Array)
            {
                foreach (var f in factors.EnumerateArray())
                {
                    var description = GetString(f, "FactorDescription");
                    if (string.IsNullOrEmpty(description)) continue;

                    _context.ContributoryFactors.Add(new ContributoryFactor
                    {
                        Crash = crash,
                        FactorCategory = GetString(f, "FactorCategory"),
                        FactorDescription = description,
                        IsMajorFactor = GetBool(f, "IsMajorFactor", false)
                    });
                }
            }


            if (root.TryGetProperty("Witnesses", out var witnesses) && witnesses.ValueKind == JsonValueKind.Array)
            {
                foreach (var w in witnesses.EnumerateArray())
                {
                    var surname = GetString(w, "Surname");
                    if (string.IsNullOrEmpty(surname)) continue;

                    _context.Witnesses.Add(new Witness
                    {
                        Crash = crash,
                        SurnameInitials = surname,
                        IdNumber = GetString(w, "IdNumber"),
                        CellPhone = GetString(w, "CellPhone"),
                        OtherPhone = GetString(w, "OtherPhone"),
                        WorkContactAddress = GetString(w, "WorkContactAddress")
                    });
                }
            }

            
            if (root.TryGetProperty("OfficialUse", out var officialUse))
            {
                var official = new OfficialUse
                {
                    Crash = crash,
                    OfficeWhereOccurred = GetString(officialUse, "OfficeWhereOccurred"),
                    OccurrenceBookNo = GetString(officialUse, "OccurrenceBookNo"),
                    AccidentRegisterNo = GetString(officialUse, "AccidentRegisterNo"),
                    SapsCasNo = GetString(officialUse, "SapsCasNo"),
                    DepartmentNameOccurred = GetString(officialUse, "DepartmentNameOccurred"),
                    InspectedByInitials = GetString(officialUse, "InspectedByInitials"),
                    InspectedByRank = GetString(officialUse, "InspectedByRank"),
                    InspectedBySurname = GetString(officialUse, "InspectedBySurname"),
                    InspectedByServiceNumber = GetString(officialUse, "InspectedByServiceNumber"),
                    InspectedBySignature = GetString(officialUse, "InspectedBySignature"),
                    OfficeWhereReported = GetString(officialUse, "OfficeWhereReported"),
                    DepartmentNameReported = GetString(officialUse, "DepartmentNameReported"),
                    CompletedBy = GetString(officialUse, "CompletedBy"),
                    CompletedInitials = GetString(officialUse, "CompletedInitials"),
                    CompletedRank = GetString(officialUse, "CompletedRank"),
                    CompletedSurname = GetString(officialUse, "CompletedSurname"),
                    CompletedServiceNumber = GetString(officialUse, "CompletedServiceNumber"),
                    CompletedSignature = GetString(officialUse, "CompletedSignature"),
                    CapturingNumber = GetString(officialUse, "CapturingNumber"),
                    Comments = GetString(officialUse, "Comments")
                };

                
                if (officialUse.TryGetProperty("DateStamp", out var dateStampEl) && dateStampEl.ValueKind == JsonValueKind.String)
                    official.DateStamp = DateOnly.TryParse(dateStampEl.GetString(), out var ds) ? ds : null;

                if (officialUse.TryGetProperty("CompletedDate", out var compDateEl) && compDateEl.ValueKind == JsonValueKind.String)
                    official.CompletedDate = DateOnly.TryParse(compDateEl.GetString(), out var cd) ? cd : null;

                if (officialUse.TryGetProperty("CompletedTime", out var compTimeEl) && compTimeEl.ValueKind == JsonValueKind.String)
                    official.CompletedTime = TimeOnly.TryParse(compTimeEl.GetString(), out var ct) ? ct : null;

                _context.OfficialUses.Add(official);
            }

            if (root.TryGetProperty("Attachments", out var attachments) && attachments.ValueKind == JsonValueKind.Array)
            {
                var uploadDir = Path.Combine(_env.WebRootPath, "uploads", "crashes", crash.CrashId.ToString());
                Directory.CreateDirectory(uploadDir);

                var attachmentNotes = GetString(root, "AttachmentNotes");

                foreach (var a in attachments.EnumerateArray())
                {
                    var fileName = GetString(a, "FileName");
                    var fileData = GetString(a, "FileData");
                    if (string.IsNullOrEmpty(fileName) || string.IsNullOrEmpty(fileData)) continue;

                    var safeName = $"{Guid.NewGuid()}_{Path.GetFileName(fileName)}";
                    var fullPath = Path.Combine(uploadDir, safeName);
                    await System.IO.File.WriteAllBytesAsync(fullPath, Convert.FromBase64String(fileData));

                    _context.CrashSketches.Add(new CrashSketch
                    {
                        Crash = crash,
                        SketchType = "attachment",
                        FilePath = $"/uploads/crashes/{crash.CrashId}/{safeName}",
                        Notes = attachmentNotes,
                        CreatedAt = DateTime.Now
                    });
                }
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return new CrashSubmitResult
            {
                Outcome = CrashSubmitOutcome.Success,
                CrashId = crash.CrashId,
                CrNo = crash.CrNo,
                ExistsAsSummary = existsAsSummary
            };
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            return new CrashSubmitResult
            {
                Outcome = CrashSubmitOutcome.SaveFailed,
                ErrorMessage = $"An error occurred: {ex.Message} | Inner: {ex.InnerException?.Message}"
            };
        }
    }

    public async Task<bool> UpdateCoreFieldsAsync(int id, Crash formValues)
    {
        if (id != formValues.CrashId) return false;
        if (!await _context.Crashes.AnyAsync(c => c.CrashId == id)) return false;

        _context.Update(formValues);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<(bool success, string? duplicateError, int crashId)> CreateCoreOnlyAsync(Crash crash)
    {
        if (!string.IsNullOrWhiteSpace(crash.CrNo) &&
            await _context.Crashes.AnyAsync(c => c.CrNo == crash.CrNo))
            return (false, $"A full accident report with AR/CR number '{crash.CrNo}' already exists.", 0);

        _context.Add(crash);
        await _context.SaveChangesAsync();
        return (true, null, crash.CrashId);
    }

    public async Task<bool> DeleteCrashAsync(int crashId)
    {
        var crash = await _context.Crashes
            .Include(c => c.CrashLocations)
            .Include(c => c.CrashConditions)
            .Include(c => c.CrashWeathers)
            .Include(c => c.CrashVehicles).ThenInclude(cv => cv.VehicleDamages)
            .Include(c => c.CrashVehicles).ThenInclude(cv => cv.CrashPeople)
            .Include(c => c.CrashPeople).ThenInclude(cp => cp.PedestrianBicyclistDetails)
            .Include(c => c.ContributoryFactors)
            .Include(c => c.DangerousGoods)
            .Include(c => c.Witnesses)
            .Include(c => c.OfficialUses)
            .Include(c => c.CrashSketches)
            .FirstOrDefaultAsync(c => c.CrashId == crashId);

        if (crash == null) return false;

        _context.CrashSketches.RemoveRange(crash.CrashSketches);
        _context.OfficialUses.RemoveRange(crash.OfficialUses);
        _context.Witnesses.RemoveRange(crash.Witnesses);
        _context.DangerousGoods.RemoveRange(crash.DangerousGoods);
        _context.ContributoryFactors.RemoveRange(crash.ContributoryFactors);
        _context.CrashWeathers.RemoveRange(crash.CrashWeathers);
        _context.CrashConditions.RemoveRange(crash.CrashConditions);
        _context.CrashLocations.RemoveRange(crash.CrashLocations);

        foreach (var cv in crash.CrashVehicles)
        {
            _context.VehicleDamages.RemoveRange(cv.VehicleDamages);
            _context.CrashPeople.RemoveRange(cv.CrashPeople);
        }
        _context.CrashVehicles.RemoveRange(crash.CrashVehicles);

        foreach (var cp in crash.CrashPeople)
            _context.PedestrianBicyclistDetails.RemoveRange(cp.PedestrianBicyclistDetails);
        _context.CrashPeople.RemoveRange(crash.CrashPeople);

        _context.Crashes.Remove(crash);
        await _context.SaveChangesAsync();
        return true;
    }

   

    private static string? GetString(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.String)
            return prop.GetString();
        return null;
    }

    private static short GetShort(JsonElement element, string propertyName, short defaultValue = 0)
    {
        if (element.TryGetProperty(propertyName, out var prop))
        {
            if (prop.ValueKind == JsonValueKind.Number)
            {
                try
                {
                    return prop.GetInt16();
                }
                catch
                {
                    
                    if (prop.TryGetInt32(out var intVal))
                    {
                        return Convert.ToInt16(Math.Min(intVal, short.MaxValue));
                    }
                    return defaultValue;
                }
            }
            if (prop.ValueKind == JsonValueKind.String)
            {
                if (short.TryParse(prop.GetString(), out var result))
                    return result;
            }
        }
        return defaultValue;
    }

    private static byte GetByte(JsonElement element, string propertyName, byte defaultValue = 0)
    {
        if (element.TryGetProperty(propertyName, out var prop))
        {
            if (prop.ValueKind == JsonValueKind.Number)
            {
                try
                {
                    return prop.GetByte();
                }
                catch
                {
                    
                    if (prop.TryGetInt32(out var intVal))
                    {
                        return Convert.ToByte(Math.Min(intVal, byte.MaxValue));
                    }
                    return defaultValue;
                }
            }
            if (prop.ValueKind == JsonValueKind.String)
            {
                if (byte.TryParse(prop.GetString(), out var result))
                    return result;
            }
        }
        return defaultValue;
    }

    private static decimal? GetDecimal(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.Number)
            return prop.GetDecimal();
        return null;
    }

    private static DateOnly? GetDateOnly(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var prop) &&
            prop.ValueKind == JsonValueKind.String &&
            DateOnly.TryParse(prop.GetString(), out var date))
        {
            return date;
        }

        return null;
    }

    private static bool GetBool(JsonElement element, string propertyName, bool defaultValue = false)
    {
        if (element.TryGetProperty(propertyName, out var prop))
        {
            if (prop.ValueKind == JsonValueKind.True || prop.ValueKind == JsonValueKind.False)
                return prop.GetBoolean();
            if (prop.ValueKind == JsonValueKind.String)
            {
                var str = prop.GetString()?.ToLower();
                return str == "true" || str == "yes" || str == "1";
            }
            if (prop.ValueKind == JsonValueKind.Number)
            {
                return prop.GetInt32() != 0;
            }
        }
        return defaultValue;
    }
}
