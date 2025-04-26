var baseMeasures = Model.AllMeasures.Where(m => (m.Name.Contains("_Count"))).ToList();

      
foreach(var measure in baseMeasures)
{
            measure.IsHidden = true;
}
