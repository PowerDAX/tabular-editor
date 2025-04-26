// Create Explicit Time Intelligence Measures
// ---------------------------------------------------------------------------------------------
// Purpose: This script creates explicit time intelligence measures for each base measure
// using the Time Intelligence calculation group. For each base measure, it creates a new measure
// for each calculation item in the Time Intelligence calculation group.
//
// Usage: Set the configuration parameters below, then run the script.
// The script will create or update measures that apply time intelligence calculations to base measures.
//
// Example: A base measure "Sales" with Time Intelligence calculation items "YTD", "PY", "YOY %"
//          would generate measures like:
//          - "Sales YTD" - the year-to-date value of Sales
//          - "Sales PY" - the prior year value of Sales
//          - "Sales YOY %" - the year-over-year percentage change of Sales
//
// Notes: - Measures are created for all base measures in the specified display folder
//        - When overwriteExistingMeasures=1, existing measures are updated rather than skipped
//        - Percentage measures automatically get percentage formatting
//        - Display folders are organized by measure name for easier navigation
//        - Updates preserve measure lineage tags (important for source control)
// ---------------------------------------------------------------------------------------------

//--------------------------------
// CONFIGURATION PARAMETERS
//--------------------------------

// Basic configuration
var overwriteExistingMeasures = 0; // 1 to update/overwrite existing measures, 0 to skip them
var baseMeasuresDisplayFolder = "Measures"; // Base folder containing measures to process
var timeIntelligenceCalculationGroup = "Time Intelligence"; // Name of the calculation group
var timeIntelligenceCalculationItem = "Time Calculation"; // Name of the calculation item column

// Target folder structure configuration
var targetDisplayFolderPrefix = "Time Intelligence"; // Prefix for the display folder
var organizeByMeasure = true; // Whether to organize by measure name in display folders

// Table and measure filtering
var includedTableNameFilters = new List<string>(); // Empty list means all tables are included

var excludeTableNamePrefixes = new List<string> {
    "Parameter", 
    "Dynamic",
    "Dim Calendar"
    // Add other table name prefixes to exclude
};

var excludeMeasureNameContains = new List<string> {
    "Growth"
    // Add other strings to exclude if measure name contains them
};

// Format string configuration
var percentageFormatString = "#,##0.00 %;(#,##0.00 %)"; // Format for percentage measures
var percentageIndicators = new List<string> { 
    "%", 
    "Percent", 
    "Pct" 
    // Add other indicators that the measure is a percentage
};

//--------------------------------
// VALIDATION
//--------------------------------

// Validate that the calculation group exists
if (!Model.Tables.Contains(timeIntelligenceCalculationGroup))
{
    Error($"Calculation group '{timeIntelligenceCalculationGroup}' does not exist in the model. Please check the configuration.");
    return;
}

// Verify that the calculation group is actually a calculation group
if (!(Model.Tables[timeIntelligenceCalculationGroup] is CalculationGroupTable))
{
    Error($"'{timeIntelligenceCalculationGroup}' is not a calculation group table. Please check the configuration.");
    return;
}

// Get the calculation group and verify it has calculation items
var calcGroup = Model.Tables[timeIntelligenceCalculationGroup] as CalculationGroupTable;
if (calcGroup.CalculationItems.Count == 0)
{
    Error($"Calculation group '{timeIntelligenceCalculationGroup}' has no calculation items. Please check the configuration.");
    return;
}

// Validate that the calculation item column exists in the calculation group
if (!calcGroup.Columns.Any(c => c.Name == timeIntelligenceCalculationItem))
{
    Error($"Calculation item column '{timeIntelligenceCalculationItem}' does not exist in calculation group '{timeIntelligenceCalculationGroup}'. Please check the configuration.");
    return;
}

//--------------------------------
// SCRIPT EXECUTION
//--------------------------------

// Get candidate base measures using filtering
var candidateBaseMeasures = Model.AllMeasures.AsQueryable();

// Filter by display folder
if (!string.IsNullOrEmpty(baseMeasuresDisplayFolder))
{
    candidateBaseMeasures = candidateBaseMeasures.Where(m => m.DisplayFolder.StartsWith(baseMeasuresDisplayFolder));
}

// Exclude hidden measures
candidateBaseMeasures = candidateBaseMeasures.Where(m => !m.IsHidden);

// Include only measures from tables that match the include filters (if any are specified)
if (includedTableNameFilters.Any())
{
    candidateBaseMeasures = candidateBaseMeasures.Where(m => 
        includedTableNameFilters.Any(filter => m.Table.Name.StartsWith(filter))
    );
}

// Exclude measures from tables with specified prefixes
if (excludeTableNamePrefixes.Any())
{
    candidateBaseMeasures = candidateBaseMeasures.Where(m => 
        !excludeTableNamePrefixes.Any(prefix => m.Table.Name.StartsWith(prefix))
    );
}

// Exclude measures with specified strings in their names
if (excludeMeasureNameContains.Any())
{
    candidateBaseMeasures = candidateBaseMeasures.Where(m => 
        !excludeMeasureNameContains.Any(text => m.Name.Contains(text))
    );
}

var baseMeasures = candidateBaseMeasures.ToList();

// Statistics counters
int created = 0, skipped = 0, updated = 0, errors = 0;

// Log start of processing
Info($"Processing {baseMeasures.Count} base measures with {calcGroup.CalculationItems.Count} time intelligence calculations");

// List measure names for reference
if (baseMeasures.Count <= 20) // Only show list if not too long
{
    Info($"Processing measures: {string.Join(", ", baseMeasures.Select(m => m.Name))}");
}

// Get list of calculation items
var calcItems = calcGroup.CalculationItems.ToList();

// List calculation items for reference
Info($"Applying calculations: {string.Join(", ", calcItems.Select(c => c.Name))}");

// Process each base measure
foreach(var measure in baseMeasures)
{
    var baseMeasureTable = measure.Table;
    
    try 
    {
        // Process each calculation item
        foreach(var calcItem in calcItems)
        {
            // Construct the new measure name
            string newMeasureName = measure.Name + " " + calcItem.Name;
            
            // Build the DAX expression with the calculation group filter
            string newExpression = "CALCULATE( " + 
                measure.DaxObjectFullName + 
                ", '" + timeIntelligenceCalculationGroup + 
                "'[" + timeIntelligenceCalculationItem + 
                "]= \"" + calcItem.Name + "\" )";
            
            // Construct the display folder path
            string newDisplayFolder;
            if (organizeByMeasure)
            {
                // Organize by measure name (e.g., "Time Intelligence\Sales")
                newDisplayFolder = measure.DisplayFolder.Replace(
                    baseMeasuresDisplayFolder, 
                    targetDisplayFolderPrefix
                ) + "\\" + measure.Name;
            }
            else
            {
                // Organize by time calculation (e.g., "Time Intelligence\YTD\Sales")
                newDisplayFolder = measure.DisplayFolder.Replace(
                    baseMeasuresDisplayFolder, 
                    targetDisplayFolderPrefix + "\\" + calcItem.Name
                );
            }
            
            // Determine whether the measure should be a percentage measure
            bool isPercentageMeasure = percentageIndicators.Any(indicator => 
                calcItem.Name.Contains(indicator)
            );
            
            // Set the format string based on whether it's a percentage measure
            string formatString = isPercentageMeasure 
                ? percentageFormatString 
                : measure.FormatString;
                
            // Check if measure already exists
            var existingMeasure = Model.AllMeasures.FirstOrDefault(m => m.Name == newMeasureName && !m.IsHidden);
            
            if (existingMeasure != null)
            {
                if (overwriteExistingMeasures == 1)
                {
                    // Update the existing measure properties instead of deleting and recreating
                    // This preserves the measure's lineage tag and reduces git diff size
                    existingMeasure.Expression = newExpression;
                    existingMeasure.DisplayFolder = newDisplayFolder;
                    existingMeasure.FormatString = formatString;
                    updated++;
                }
                else
                {
                    // Skip if overwrite is disabled
                    skipped++;
                }
            }
            else
            {
                // Create new measure
                var newMeasure = baseMeasureTable.AddMeasure(newMeasureName, newExpression);
                newMeasure.DisplayFolder = newDisplayFolder;
                newMeasure.FormatString = formatString;
                created++;
            }
        }
    }
    catch (Exception ex)
    {
        // Log any errors that occur during processing
        Error($"Error processing measure '{measure.Name}': {ex.Message}");
        errors++;
    }
}

// Output summary statistics
Info($"---- SUMMARY ----");
Info($"Time Intelligence Measures Created: {created}");
if (overwriteExistingMeasures == 1)
{
    Info($"Time Intelligence Measures Updated: {updated}");
}
else
{
    Info($"Time Intelligence Measures Skipped (already exist): {skipped}");
}

if (errors > 0)
{
    Error($"Errors Encountered: {errors}");
} 