DELETE FROM vehicle_damage;
DELETE FROM pedestrian_bicyclist_details;
DELETE FROM drivers_licences;


DELETE FROM crash_sketches;
DELETE FROM official_use;
DELETE FROM witnesses;
DELETE FROM dangerous_goods;
DELETE FROM contributory_factors;
DELETE FROM crash_weather;


DELETE FROM crash_persons;
DELETE FROM crash_vehicles;


DELETE FROM crash_conditions;
DELETE FROM crash_locations;


DELETE FROM crash_demographics;


DELETE FROM crashes;
DELETE FROM persons;
DELETE FROM vehicles;


DBCC CHECKIDENT ('vehicle_damage',           RESEED, 0);
DBCC CHECKIDENT ('pedestrian_bicyclist_details', RESEED, 0);
DBCC CHECKIDENT ('drivers_licences',         RESEED, 0);
DBCC CHECKIDENT ('crash_sketches',           RESEED, 0);
DBCC CHECKIDENT ('official_use',             RESEED, 0);
DBCC CHECKIDENT ('witnesses',                RESEED, 0);
DBCC CHECKIDENT ('dangerous_goods',          RESEED, 0);
DBCC CHECKIDENT ('contributory_factors',     RESEED, 0);
DBCC CHECKIDENT ('crash_weather',            RESEED, 0);
DBCC CHECKIDENT ('crash_persons',            RESEED, 0);
DBCC CHECKIDENT ('crash_vehicles',           RESEED, 0);
DBCC CHECKIDENT ('crash_conditions',         RESEED, 0);
DBCC CHECKIDENT ('crash_locations',          RESEED, 0);
DBCC CHECKIDENT ('crash_demographics',       RESEED, 0);
DBCC CHECKIDENT ('crashes',                  RESEED, 0);
DBCC CHECKIDENT ('persons',                  RESEED, 0);
DBCC CHECKIDENT ('vehicles',                 RESEED, 0);

