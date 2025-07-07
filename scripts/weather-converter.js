#!/usr/bin/env node

const fs = require('fs');
const path = require('path');

/**
 * Converts a string to camelCase
 * @param {string} str - The string to convert
 * @returns {string} The camelCase string
 */
function toCamelCase(str) {
    return str.toLowerCase().replace(/_([a-z])/g, (match, letter) => letter.toUpperCase());
}

/**
 * Parses intensity values in format "min,max" to object
 * @param {string} value - The intensity value string
 * @returns {object|number} Object with min/max or single number
 */
function parseIntensity(value) {
    if (value.includes(',')) {
        const [min, max] = value.split(',').map(v => parseInt(v.trim()));
        return { min, max };
    }
    return parseInt(value);
}

/**
 * Parses WeatherAB DFN content and converts it to JSON structure
 * @param {string} content - The DFN file content
 * @returns {object} The parsed JSON structure
 */
function parseWeatherABToJSON(content) {
    const result = {
        header: {
            title: "UOX3 DFNs",
            repository: "https://github.com/UOX3DevTeam/UOX3",
            lastUpdate: "1/4/2003",
            script: "weatherab.dfn",
            description: "Weather definitions for UOX3 regions. Referenced from regions.dfn as ABWEATH=..."
        },
        weatherTypes: []
    };

    /// Remove comments and split into lines
    const lines = content.split('\n');
    const cleanLines = lines
        .map(line => line.trim())
        .filter(line => line && !line.startsWith('//') && !line.startsWith('EOF'));

    let currentSection = null;
    let currentData = {};
    let sectionComment = '';

    for (let i = 0; i < cleanLines.length; i++) {
        const line = cleanLines[i];

        /// Check for section headers
        if (line.startsWith('[WEATHERAB ') && line.endsWith(']')) {
            /// Save previous section if exists
            if (currentSection && Object.keys(currentData).length > 0) {
                saveWeatherSection(result, currentSection, currentData, sectionComment);
            }

            /// Start new section
            currentSection = line;
            currentData = {};
            sectionComment = extractComment(lines, i);
            continue;
        }

        /// Check for section end
        if (line === '}') {
            if (currentSection && Object.keys(currentData).length > 0) {
                saveWeatherSection(result, currentSection, currentData, sectionComment);
            }
            currentSection = null;
            currentData = {};
            sectionComment = '';
            continue;
        }

        /// Skip opening braces
        if (line === '{') {
            continue;
        }

        /// Parse key-value pairs
        if (line.includes('=')) {
            const equalIndex = line.indexOf('=');
            const key = line.substring(0, equalIndex).trim();
            const value = line.substring(equalIndex + 1).trim();

            const camelKey = toCamelCase(key);

            /// Handle special intensity properties with min,max values
            if (['RAININTENSITY', 'SNOWINTENSITY', 'STORMINTENSITY'].includes(key)) {
                currentData[camelKey] = parseIntensity(value);
            } else {
                /// Convert values based on type
                if (value === '0' || value === '1') {
                    /// Convert 0/1 to boolean
                    currentData[camelKey] = value === '1';
                } else if (/^\d+$/.test(value)) {
                    /// Convert other numeric values to int
                    currentData[camelKey] = parseInt(value);
                } else {
                    /// Keep as string
                    currentData[camelKey] = value;
                }
            }
        }
    }

    /// Save last section
    if (currentSection && Object.keys(currentData).length > 0) {
        saveWeatherSection(result, currentSection, currentData, sectionComment);
    }

    return result;
}

/**
 * Extracts comment from the line before section header
 * @param {string[]} lines - All lines from the file
 * @param {number} index - Current line index
 * @returns {string} The extracted comment or empty string
 */
function extractComment(lines, index) {
    /// Look for comment in the few lines before the section
    for (let i = index - 1; i >= Math.max(0, index - 3); i--) {
        const line = lines[i].trim();
        if (line.startsWith('//') && !line.includes('*') && !line.includes('#') && !line.includes('WEATHER')) {
            const comment = line.substring(2).trim();
            /// Skip technical comments and take only descriptive ones
            if (comment.length > 0 && comment.length < 20 && !comment.includes('=') && !comment.includes('chance')) {
                return comment;
            }
        }
    }
    return '';
}

/**
 * Saves a weather section to the result object
 * @param {object} result - The result object
 * @param {string} sectionHeader - The section header
 * @param {object} data - The section data
 * @param {string} comment - The section comment
 */
function saveWeatherSection(result, sectionHeader, data, comment) {
    const match = sectionHeader.match(/\[WEATHERAB (\d+)\]/);
    if (match) {
        const weatherId = parseInt(match[1]);

        const weatherData = {
            id: weatherId,
            ...data
        };

        /// Add comment if exists
        if (comment) {
            weatherData.description = comment;
        }

        /// Add weather type name based on ID and properties
        weatherData.name = getWeatherTypeName(weatherId, data, comment);

        result.weatherTypes.push(weatherData);
    }
}

/**
 * Determines weather type name based on ID and properties
 * @param {number} id - Weather ID
 * @param {object} data - Weather data
 * @param {string} comment - Section comment
 * @returns {string} Weather type name
 */
function getWeatherTypeName(id, data, comment) {
    /// Use comment if it's descriptive and clean
    if (comment && comment.length > 0 && comment.length < 20 && !comment.includes('*') && !comment.includes('#')) {
        return comment.charAt(0).toUpperCase() + comment.slice(1);
    }

    /// Determine based on ID - hardcoded names from file analysis
    switch (id) {
        case 0: return 'No Weather';
        case 1: return 'Desert';
        case 2: return 'Tropical';
        case 3: return 'Plains';
        case 4: return 'Northern Mountain';
        case 5: return 'Southern Mountain';
        case 6: return 'British';
        case 7: return 'Arctic';
        case 8: return 'Forest';
        default: return `Weather Type ${id}`;
    }
}

/**
 * Main function to convert WeatherAB DFN file to JSON
 * @param {string} inputFile - Path to the input DFN file
 * @param {string} outputFile - Path to the output JSON file
 */
function convertWeatherABToJSON(inputFile, outputFile) {
    try {
        console.log(`Reading file: ${inputFile}`);
        const content = fs.readFileSync(inputFile, 'utf8');

        console.log('Parsing WeatherAB DFN content...');
        const jsonResult = parseWeatherABToJSON(content);

        console.log('Converting to JSON...');
        const jsonString = JSON.stringify(jsonResult, null, 2);

        console.log(`Writing to file: ${outputFile}`);
        fs.writeFileSync(outputFile, jsonString, 'utf8');

        console.log('\n✅ Conversion completed successfully!');
        console.log(`📊 Statistics:`);
        console.log(`   - Weather Types: ${jsonResult.weatherTypes.length}`);
        console.log(`   - Output file size: ${(jsonString.length / 1024).toFixed(2)} KB`);

        /// Show example of weather type
        const exampleWeather = jsonResult.weatherTypes.find(w => w.id === 1);
        if (exampleWeather) {
            console.log(`\n🌤️ Weather type example:`);
            console.log(`   - ${exampleWeather.name}: rainChance = ${exampleWeather.rainChance} (${typeof exampleWeather.rainChance})`);
            if (exampleWeather.rainIntensity && typeof exampleWeather.rainIntensity === 'object') {
                console.log(`   - Rain intensity: min=${exampleWeather.rainIntensity.min}, max=${exampleWeather.rainIntensity.max}`);
            } else {
                console.log(`   - Rain intensity: ${exampleWeather.rainIntensity}`);
            }
        }

    } catch (error) {
        console.error('❌ Error during conversion:', error.message);
        process.exit(1);
    }
}

/// Command line interface
function main() {
    const args = process.argv.slice(2);

    if (args.length === 0) {
        console.log('UOX3 WeatherAB DFN to JSON Converter');
        console.log('Usage: node weatherab-converter.js <input.dfn> [output.json]');
        console.log('');
        console.log('Examples:');
        console.log('  node weatherab-converter.js weatherab.dfn');
        console.log('  node weatherab-converter.js weatherab.dfn weather.json');
        console.log('  node weatherab-converter.js input/weatherab.dfn output/weather.json');
        process.exit(1);
    }

    const inputFile = args[0];
    let outputFile = args[1];

    /// Generate output filename if not provided
    if (!outputFile) {
        const parsedPath = path.parse(inputFile);
        outputFile = path.join(parsedPath.dir, parsedPath.name + '.json');
    }

    /// Check if input file exists
    if (!fs.existsSync(inputFile)) {
        console.error(`❌ Input file does not exist: ${inputFile}`);
        process.exit(1);
    }

    /// Create output directory if it doesn't exist
    const outputDir = path.dirname(outputFile);
    if (!fs.existsSync(outputDir)) {
        fs.mkdirSync(outputDir, { recursive: true });
    }

    /// Perform conversion
    convertWeatherABToJSON(inputFile, outputFile);
}

/// Export functions for use as module
module.exports = {
    parseWeatherABToJSON,
    convertWeatherABToJSON,
    toCamelCase,
    parseIntensity
};

/// Run main function if script is executed directly
if (require.main === module) {
    main();
}
