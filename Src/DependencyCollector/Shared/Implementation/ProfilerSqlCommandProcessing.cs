﻿namespace Microsoft.ApplicationInsights.DependencyCollector.Implementation
{
    using System.Data;
    using System.Data.SqlClient;
    using Microsoft.ApplicationInsights.DependencyCollector.Implementation.Operation;
    using Microsoft.ApplicationInsights.Extensibility;

    /// <summary>
    /// Concrete class with all processing logic to generate RDD data from the calls backs
    /// received from Profiler instrumentation for SQL Command.    
    /// </summary>
    internal sealed class ProfilerSqlCommandProcessing : ProfilerSqlProcessingBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ProfilerSqlCommandProcessing"/> class.
        /// </summary>
        internal ProfilerSqlCommandProcessing(TelemetryConfiguration configuration, string agentVersion, ObjectInstanceBasedOperationHolder telemetryTupleHolder)
            : base(configuration, agentVersion, telemetryTupleHolder)
        {            
        }

        /// <summary>
        /// Gets SQL command resource name.
        /// </summary>
        /// <param name="thisObj">The SQL command.</param>
        /// <remarks>Before we have clarity with SQL team around EventSource instrumentation, providing name as a concatenation of parameters.</remarks>
        /// <returns>The resource name if possible otherwise empty string.</returns>
        internal override string GetResourceName(object thisObj)
        {
            SqlCommand command = thisObj as SqlCommand;
            string resource = string.Empty;
            if (command != null)
            {
                if (command.Connection != null)
                {
                    string commandName = command.CommandType == CommandType.StoredProcedure
                        ? command.CommandText
                        : string.Empty;

                    resource = string.IsNullOrEmpty(commandName)
                        ? string.Join(" | ", command.Connection.DataSource, command.Connection.Database)
                        : string.Join(" | ", command.Connection.DataSource, command.Connection.Database, commandName);
                }
            }

            return resource;
        }

        /// <summary>
        /// Gets SQL resource target name.
        /// </summary>
        /// <param name="thisObj">The SQL command.</param>
        /// <returns>The resource target name if possible otherwise empty string.</returns>
        internal override string GetResourceTarget(object thisObj)
        {
            SqlCommand command = thisObj as SqlCommand;
            string result = string.Empty;
            if (command != null)
            {
                if (command.Connection != null)
                {
                    result = string.Join(" | ", command.Connection.DataSource, command.Connection.Database);
                }
            }

            return result;
        }

        /// <summary>
        /// Gets SQL resource command text.
        /// </summary>
        /// <param name="thisObj">The SQL command.</param>
        /// <returns>Returns the command text or empty.</returns>
        internal override string GetCommandName(object thisObj)
        {
            SqlCommand command = thisObj as SqlCommand;

            if (command != null)
            {
                return command.CommandText ?? string.Empty;
            }

            return string.Empty;
        }
    }
}