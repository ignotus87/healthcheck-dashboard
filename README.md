# healthcheck-dashboard
A tool to show the health state of a range of services, jobs and other KPIs.
Works as a task scheduler that runs Tasks to determine the State of
Resources using Conditions.

# Concepts
* The tool should have a console interface, later extensible with a gui
* Aims a Windows network environment

# Resources
The entities that we want to check. Should handle various resources, eg.
* file (general) - last modified date, file size etc.
* file (text) - nth row contains value, number of rows etc.
* sql query results: column value in nth row in mth datatable
* return values of executables
* file (excel)
* web service calls
* date of the last modified file in a folder

# Tasks
Tasks have a Schedule and a ReturnedValue. 
The Schedule tells when to ferform the check.
The ReturnedValue has the last result, that is the subjet of conditions.
ReturnValues have types and should also have extensible converters

# ReturnValues
Should always be a single value, eg.
For a database query resultset
* a field value/max/min in the returned dataset
* number of returned rows/datatables

# Conditions
Conditions are run when a Task has finished or timed out.
For each contition, a state has to be assigned from the StateSchema of the Resource.
For date typed ReturnValue:
* elapsed time in days/hours/mins/secs/msecs lt/le/gt/ge/between, with macro rightvalues like Now(), DateAdd()
For the number of results:
* calling an api that returns some value or none
For numeric types:
* value lt/le/eq/gt/ge/between, with value macros like Round()

# States
States can be customized with state schemas, with the default one having just having healthy and un-healthy
