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
 * Parses DFN content and converts it to JSON structure
 * @param {string} content - The DFN file content
 * @returns {object} The parsed JSON structure
 */
function parseDFNToJSON(content) {
    const result = {
        header: {
            title: "UOX3 DFNs",
            repository: "https://github.com/UOX3DevTeam/UOX3",
            lastUpdate: "17 March, 2012",
            script: "regions.dfn",
            description: "Switches GUARDED,MARK,GATE,RECALL,ESCORTS,MAGICDAMAGE: 0=off/no; 1=on/yes. Set weather with ABWEATH from weather.dfn file."
        },
        regions: [],
        musicLists: [],
        instalog: {
            areas: []
        },
        sosAreas: {
            areas: []
        }
    };

    /// Remove comments and split into lines
    const lines = content.split('\n');
    const cleanLines = lines
        .map(line => line.trim())
        .filter(line => line && !line.startsWith('//') && !line.startsWith('EOF'));

    let currentSection = null;
    let currentData = {};
    let coordinates = [];
    let sectionComment = '';

    for (let i = 0; i < cleanLines.length; i++) {
        const line = cleanLines[i];

        /// Check for section headers
        if (line.startsWith('[') && line.endsWith(']')) {
            /// Save previous section if exists
            if (currentSection && (Object.keys(currentData).length > 0 || coordinates.length > 0)) {
                saveSection(result, currentSection, currentData, coordinates, sectionComment);
            }

            /// Start new section
            currentSection = line;
            currentData = {};
            coordinates = [];
            sectionComment = extractComment(lines, i);
            continue;
        }

        /// Check for section end
        if (line === '}') {
            if (currentSection && (Object.keys(currentData).length > 0 || coordinates.length > 0)) {
                saveSection(result, currentSection, currentData, coordinates, sectionComment);
            }
            currentSection = null;
            currentData = {};
            coordinates = [];
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

            /// Handle coordinate pairs
            if (['X1', 'Y1', 'X2', 'Y2'].includes(key)) {
                if (!coordinates.length || coordinates[coordinates.length - 1].hasOwnProperty('y2')) {
                    coordinates.push({});
                }
                const coordIndex = coordinates.length - 1;

                if (key === 'X1') coordinates[coordIndex].x1 = parseInt(value);
                else if (key === 'Y1') coordinates[coordIndex].y1 = parseInt(value);
                else if (key === 'X2') coordinates[coordIndex].x2 = parseInt(value);
                else if (key === 'Y2') coordinates[coordIndex].y2 = parseInt(value);
            } else {
                const camelKey = toCamelCase(key);
                /// Special case: convert WORLD to mapId
                const finalKey = camelKey === 'world' ? 'mapId' : camelKey;

                /// Properties that should remain as numbers even if they are 0 or 1
                const numericProperties = ['musicList', 'musiclist', 'abweath', 'abWeather', 'instanceId', 'instanceid', 'mapId', 'world'];

                /// Convert values based on type
                if ((value === '0' || value === '1') && !numericProperties.includes(finalKey)) {
                    /// Convert 0/1 to boolean only for boolean properties
                    currentData[finalKey] = value === '1';
                } else if (/^\d+$/.test(value)) {
                    /// Convert numeric values to int
                    currentData[finalKey] = parseInt(value);
                } else {
                    /// Keep as string
                    currentData[finalKey] = value;
                }
            }
        }
    }

    /// Save last section
    if (currentSection && (Object.keys(currentData).length > 0 || coordinates.length > 0)) {
        saveSection(result, currentSection, currentData, coordinates, sectionComment);
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
    if (index > 0) {
        const prevLine = lines[index - 1].trim();
        if (prevLine.startsWith('//')) {
            return prevLine.substring(2).trim();
        }
    }
    return '';
}

/**
 * Saves a section to the appropriate part of the result object
 * @param {object} result - The result object
 * @param {string} sectionHeader - The section header
 * @param {object} data - The section data
 * @param {Array} coordinates - The coordinates array
 * @param {string} comment - The section comment
 */
function saveSection(result, sectionHeader, data, coordinates, comment) {
    /// Add coordinates to data if they exist
    if (coordinates.length > 0) {
        data.coordinates = coordinates;
    }

    /// Add comment if exists
    if (comment) {
        data.description = comment;
    }

    if (sectionHeader.startsWith('[REGION ')) {
        const match = sectionHeader.match(/\[REGION (\d+)\]/);
        if (match) {
            const regionId = parseInt(match[1]);
            result.regions.push({
                id: regionId,
                ...data
            });
        }
    } else if (sectionHeader.startsWith('[MUSICLIST')) {
        const listName = sectionHeader.replace(/[\[\]]/g, '');

        /// Extract ID from MUSICLIST name (e.g., "MUSICLIST 1" -> 1)
        const idMatch = listName.match(/MUSICLIST\s+(\d+)/);
        const musicListId = idMatch ? parseInt(idMatch[1]) : null;

        const musicListData = {
            name: listName,
            ...data
        };

        /// Add ID if found
        if (musicListId !== null) {
            musicListData.id = musicListId;
        }

        result.musicLists.push(musicListData);
    } else if (sectionHeader === '[INSTALOG]') {
        if (coordinates.length > 0) {
            result.instalog.areas = coordinates.map(coord => ({
                ...coord,
                mapId: data.mapId || data.world || 0,
                instanceId: data.instanceId || data.instanceid || 0
            }));
        }
        if (data.description) {
            result.instalog.description = data.description;
        }
    } else if (sectionHeader === '[SOSAREAS]') {
        if (coordinates.length > 0) {
            result.sosAreas.areas = coordinates.map(coord => ({
                ...coord,
                mapId: data.mapId || data.world || 0,
                instanceId: data.instanceId || data.instanceid || 0
            }));
        }
        if (data.description) {
            result.sosAreas.description = data.description;
        }
    }
    /// Skip MUSICLIST sections - we don't need them in the output
}

/**
 * Processes multiple MUSIC entries for the same musiclist
 * @param {object} data - The current data object
 * @param {string} key - The property key
 * @param {*} value - The property value
 */
function handleMusicEntry(data, key, value) {
    if (!data[key]) {
        data[key] = [];
    }
    if (Array.isArray(data[key])) {
        data[key].push(value);
    } else {
        data[key] = [data[key], value];
    }
}

/**
 * Main function to convert DFN file to JSON
 * @param {string} inputFile - Path to the input DFN file
 * @param {string} outputFile - Path to the output JSON file
 */
function convertDFNToJSON(inputFile, outputFile) {
    try {
        console.log(`Reading file: ${inputFile}`);
        const content = fs.readFileSync(inputFile, 'utf8');

        console.log('Parsing DFN content...');
        const jsonResult = parseDFNToJSON(content);

        console.log('Converting to JSON...');
        const jsonString = JSON.stringify(jsonResult, null, 2);

        console.log(`Writing to file: ${outputFile}`);
        fs.writeFileSync(outputFile, jsonString, 'utf8');

        console.log('\n✅ Conversion completed successfully!');
        console.log(`📊 Statistics:`);
        console.log(`   - Regions: ${jsonResult.regions.length}`);
        console.log(`   - Music Lists: ${jsonResult.musicLists.length}`);
        console.log(`   - Instalog Areas: ${jsonResult.instalog.areas.length}`);
        console.log(`   - SOS Areas: ${jsonResult.sosAreas.areas.length}`);
        console.log(`   - Output file size: ${(jsonString.length / 1024).toFixed(2)} KB`);

        /// Show example of boolean conversion
        const exampleRegion = jsonResult.regions.find(r => r.guarded !== undefined);
        if (exampleRegion) {
            console.log(`\n🔄 Boolean conversion example:`);
            console.log(`   - Region "${exampleRegion.name}": guarded = ${exampleRegion.guarded} (${typeof exampleRegion.guarded})`);
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
        console.log('UOX3 DFN to JSON Converter');
        console.log('Usage: node dfn-converter.js <input.dfn> [output.json]');
        console.log('');
        console.log('Examples:');
        console.log('  node dfn-converter.js regions.dfn');
        console.log('  node dfn-converter.js regions.dfn regions.json');
        console.log('  node dfn-converter.js input/regions.dfn output/regions.json');
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
    convertDFNToJSON(inputFile, outputFile);
}

/// Export functions for use as module
module.exports = {
    parseDFNToJSON,
    convertDFNToJSON,
    toCamelCase
};

/// Run main function if script is executed directly
if (require.main === module) {
    main();
}
