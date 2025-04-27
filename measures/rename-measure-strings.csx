// Multi-string Search and Replace in Measure Names with Dependency Updates
// ---------------------------------------------------------------------------------------------
// Author: Adam Harper (enhanced by Claude)
//
// This script iterates through measures, using the matched pairs of strings
// in the 2-D array named "ReplacementPair" below as a template, replacing all instances of "FromStringX"
// in the current measure name with the corresponding "ToStringX".
//
// The script also updates all downstream dependencies that reference the renamed measures:
// - References in other measure expressions
// - String literals in calculation items (like SELECTEDVALUE checks)
// - DAX expressions containing measure references
// ---------------------------------------------------------------------------------------------
//
// Replace Text Strings Pairs that appear in selected measures
// ---------------------------------------------------------------------------------------------
//
// Update the value of the text arrays for your desired usecase. Make sure to structure the list as:
// var ReplacementPair  = new string[,] {{"FromString1","ToString1"},{"FromString2","ToString2"},{"FromString3","ToString3"}};
//
// Add as many From and To pairs to the array as needed. 
// (technically C# has a 2GB memory size and 4 Billion array item limit, but... really...)
// ---------------------------------------------------------------------------------------------
// If the string you are either searching for or replacing with contains a double quote " then you need to 'escape it' by 
// preceding it with a backslash (as in \") to have that quote character within the respective text string
// ---------------------------------------------------------------------------------------------
 
var ReplacementPair = new string[,] { {"string1","newString1"},
                                      {"string2","newString2"},
                                      {"string3","newString3"}};

//--------------------------------
// CONFIGURATION
//--------------------------------

// Tables to filter measures for renaming (empty means all tables)
var includedTableNames = new List<string> { "tableName" };
  
// Whether to include hidden measures
var includeHiddenMeasures = false;

// Whether to update calculation group calculation items with SELECTEDVALUE checks
var updateCalculationItems = true;

// Table name of dynamic measures calculation group (if any)
var dynamicMeasuresTableName = "Dynamic Measures";

// Column name containing measure names in dynamic measures table
var selectMeasureColumnName = "Select a Measure";

// Only preview changes without applying them (dry run)
var previewOnly = false;

//--------------------------------
// SCRIPT EXECUTION
//--------------------------------

if (previewOnly) {
    Info("*** PREVIEW MODE: Changes will not be applied ***");
}

// Statistics tracking
int measuresRenamed = 0;
int expressionsUpdated = 0;
int calcItemsUpdated = 0;

// Create a dictionary to store original and new measure names
var measureNameMap = new Dictionary<string, string>();

// First pass: Rename all measures based on string replacement
Info("Phase 1: Renaming measures based on string replacements...");

// Build the measure collection to process based on table filter
var measuresToProcess = includedTableNames.Count > 0 
    ? Model.AllMeasures.Where(m => includedTableNames.Any(t => m.Table.Name.StartsWith(t)))
    : Model.AllMeasures;

// Apply hidden filter if configured
if (!includeHiddenMeasures) {
    measuresToProcess = measuresToProcess.Where(m => !m.IsHidden);
}

foreach (var measure in measuresToProcess) 
{
    string originalName = measure.Name;
    string newName = originalName;
    
    // Apply all string replacements
    bool nameChanged = false;
    for (int i = 0; i < ReplacementPair.GetLength(0); i++)
    {
        if (newName.Contains(ReplacementPair[i,0])) {
            newName = newName.Replace(ReplacementPair[i,0], ReplacementPair[i,1]);
            nameChanged = true;
        }
    }
    
    // If the name changed, store the mapping and rename the measure
    if (nameChanged) {
        measureNameMap[originalName] = newName;
        measuresRenamed++;
        
        if (!previewOnly) {
            measure.Name = newName;
        }
        
        Info($"{(previewOnly ? "Would rename" : "Renamed")} measure: '{originalName}' → '{newName}'");
    }
}

// Early exit if no measures were renamed
if (measuresRenamed == 0) {
    Info("No measures would be renamed. Exiting.");
    return;
}

// Second pass: Update measure references in expressions
if (!previewOnly) {
    Info("Phase 2: Updating measure references in expressions...");
    
    // Helper function to update measure references in an expression
    string UpdateMeasureReferences(string expression) {
        if (string.IsNullOrEmpty(expression)) return expression;
        
        string updatedExpression = expression;
        foreach (var entry in measureNameMap) {
            // Replace direct measure references [MeasureName] with [NewMeasureName]
            updatedExpression = updatedExpression.Replace("[" + entry.Key + "]", "[" + entry.Value + "]");
        }
        
        return updatedExpression;
    }
    
    // Helper function to update string literals in an expression
    string UpdateStringLiterals(string expression) {
        if (string.IsNullOrEmpty(expression)) return expression;
        
        string updatedExpression = expression;
        foreach (var entry in measureNameMap) {
            // Look for pattern: SELECTEDVALUE ( 'TableName'[ColumnName] ) = "MeasureName"
            // We need to be careful to only replace exact measure name matches within quotes
            updatedExpression = updatedExpression.Replace("\"" + entry.Key + "\"", "\"" + entry.Value + "\"");
        }
        
        return updatedExpression;
    }
    
    // Update all measure expressions
    foreach (var measure in Model.AllMeasures) {
        string originalExpression = measure.Expression;
        string updatedExpression = UpdateMeasureReferences(originalExpression);
        
        if (originalExpression != updatedExpression) {
            measure.Expression = updatedExpression;
            expressionsUpdated++;
        }
    }
    
    // Update calculation items if configured
    if (updateCalculationItems) {
        Info("Phase 3: Updating calculation item references...");
        
        // Find all calculation groups
        var calculationGroups = Model.Tables
            .Where(t => t.ObjectType == ObjectType.CalculationGroupTable)
            .Cast<CalculationGroupTable>();
        
        foreach (var calcGroup in calculationGroups) {
            foreach (var calcItem in calcGroup.CalculationItems) {
                // Update measure references in the main expression
                string originalExpression = calcItem.Expression;
                string updatedExpression = UpdateMeasureReferences(originalExpression);
                updatedExpression = UpdateStringLiterals(updatedExpression);
                
                if (originalExpression != updatedExpression) {
                    calcItem.Expression = updatedExpression;
                    calcItemsUpdated++;
                }
                
                // Update format string expression if it exists
                string originalFormatExpression = calcItem.FormatStringExpression;
                string updatedFormatExpression = UpdateMeasureReferences(originalFormatExpression);
                updatedFormatExpression = UpdateStringLiterals(updatedFormatExpression);
                
                if (originalFormatExpression != updatedFormatExpression) {
                    calcItem.FormatStringExpression = updatedFormatExpression;
                    calcItemsUpdated++;
                }
            }
        }
    }
} else {
    // In preview mode, estimate the number of expression and calculation item updates
    Info("Phase 2-3: In preview mode, expressions and calculation items will be processed when actually run");
    
    // Estimate expression updates
    var potentialExpressionUpdates = Model.AllMeasures
        .Count(m => !string.IsNullOrEmpty(m.Expression) && 
               measureNameMap.Keys.Any(oldName => m.Expression.Contains("[" + oldName + "]")));
    
    // Estimate calculation item updates
    var potentialCalcItemUpdates = 0;
    if (updateCalculationItems) {
        var calculationGroups = Model.Tables
            .Where(t => t.ObjectType == ObjectType.CalculationGroupTable)
            .Cast<CalculationGroupTable>();
            
        foreach (var calcGroup in calculationGroups) {
            foreach (var calcItem in calcGroup.CalculationItems) {
                if (!string.IsNullOrEmpty(calcItem.Expression) && 
                    (measureNameMap.Keys.Any(oldName => 
                        calcItem.Expression.Contains("[" + oldName + "]") || 
                        calcItem.Expression.Contains("\"" + oldName + "\"")))) {
                    potentialCalcItemUpdates++;
                }
                
                if (!string.IsNullOrEmpty(calcItem.FormatStringExpression) && 
                    (measureNameMap.Keys.Any(oldName => 
                        calcItem.FormatStringExpression.Contains("[" + oldName + "]") || 
                        calcItem.FormatStringExpression.Contains("\"" + oldName + "\"")))) {
                    potentialCalcItemUpdates++;
                }
            }
        }
    }
    
    Info($"Approximately {potentialExpressionUpdates} measure expressions would be updated");
    if (updateCalculationItems) {
        Info($"Approximately {potentialCalcItemUpdates} calculation item expressions would be updated");
    }
    
    expressionsUpdated = potentialExpressionUpdates;
    calcItemsUpdated = potentialCalcItemUpdates;
}

// Output summary
Info("---- SUMMARY ----");
if (previewOnly) {
    Info($"Measures That Would Be Renamed: {measuresRenamed}");
    Info($"Measure Expressions That Would Be Updated: ~{expressionsUpdated}");
    if (updateCalculationItems) {
        Info($"Calculation Items That Would Be Updated: ~{calcItemsUpdated}");
    }
    Info("No changes were applied (preview mode)");
} else {
    Info($"Measures Renamed: {measuresRenamed}");
    Info($"Measure Expressions Updated: {expressionsUpdated}");
    if (updateCalculationItems) {
        Info($"Calculation Items Updated: {calcItemsUpdated}");
    }
}

// Report on any potential issues
if (measuresRenamed > 0 && expressionsUpdated == 0 && calcItemsUpdated == 0) {
    Warning("Measures were renamed but no references were updated. This might indicate that the measures are not used in any expressions.");
}
