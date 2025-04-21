// altered to allow overwrite
// altered for our environment
// altered to set format strings properly
// altered to use all base measures from all tables

// variables
// 1 overwrites existing measures, 0 preserves existing measures
// proposed enhancement: utilize an annotation to determine if a measure should be overwritten. this would allow you to overwrite all measures except for those annotated to not overwrite.
var overwriteExistingMeasures = 0;
// enter your calculation group
// proposed enhancement: change to a list to allow for multiple calculation groups
var timeIntelligenceCalculationGroup = "Time Intelligence";
var timeIntelligenceCalculationItem = "Time Calculation";

// create explicit measures from calculation items for selected base measures
var baseMeasures = Selected.Measures.ToList();

foreach(var measure in baseMeasures)
{
    var baseMeasureTable = measure.Table;
    {
        foreach(var calcItem in (Model.Tables[timeIntelligenceCalculationGroup] as CalculationGroupTable).CalculationItems)
        {
            
            // measure name
            string measureName = measure.Name + " " + calcItem.Name;

            if( overwriteExistingMeasures == 1 )
            {
                foreach( var m in Model.AllMeasures.Where( m => m.Name == measureName ).ToList() ) 
                {
                    m.Delete();
                }
            }

            // only if the measure is not yet there (think of reruns)
            if(!Model.AllMeasures.Any(x => x.Name == measureName && !x.IsHidden))
            {
                // add measure
                var newMeasure = baseMeasureTable.AddMeasure(
                    // measure name = base measure name + calculation item name
                    measure.Name + " " + calcItem.Name,
                    // dax expression = CALCULATE( base measure, calculation group = calculation item )
                    "CALCULATE( " + measure.DaxObjectFullName + ", '" + timeIntelligenceCalculationGroup + "'[" + timeIntelligenceCalculationItem + "]= \"" + calcItem.Name + "\" )"
                );

                // set display folder = calculation group name + measure name
                newMeasure.DisplayFolder = measure.DisplayFolder.Replace(baseMeasuresDisplayFolder,timeIntelligenceCalculationGroup) + "\\" + measure.Name;
    
                // set format string = % if name includes % (i.e. YOY %, etc.) or to the format string of the base measure
                if(calcItem.Name.Contains("%"))
                {
                    newMeasure.FormatString = "#,##0.00 %;(#,##0.00 %)";
                } else {
                    newMeasure.FormatString = measure.FormatString;
                }
            }
        }
    }
}