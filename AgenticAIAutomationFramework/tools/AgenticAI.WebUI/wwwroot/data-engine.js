/**
 * Advanced Data-Driven Testing Engine
 * Orchestrates multi-source data loading, validation, transformation, and analytics
 */

class DataDrivenEngine {
    constructor() {
        this.dataSources = new Map();
        this.datasets = new Map();
        this.validationRules = new Map();
        this.transformers = new Map();
        this.executionHistory = [];
        this.analytics = {
            executionCount: 0,
            successCount: 0,
            failureCount: 0,
            datasetMetrics: new Map(),
            flakyDatasets: new Set(),
            failureFrequency: new Map()
        };
        this.dataVersions = new Map();
        this.tagIndex = new Map();
        this.generatedData = [];
        this.maskingRules = new Map();
        this.datasetCoverage = new Map();
    }

    /**
     * 1️⃣ Multi-Source Data Engine - Register and manage data sources
     */
    async registerDataSource(sourceId, type, config) {
        this.dataSources.set(sourceId, { type, config, loadedAt: new Date() });
        console.log(`[DataEngine] Registered data source: ${sourceId} (${type})`);
        return { success: true, sourceId };
    }

    /**
     * Load datasets from registered sources
     */
    async loadDatasets(sourceId) {
        const source = this.dataSources.get(sourceId);
        if (!source) return { success: false, error: 'Source not found' };

        try {
            let data = [];
            switch (source.type) {
                case 'json':
                    data = await this._loadJSON(source.config);
                    break;
                case 'csv':
                    data = await this._loadCSV(source.config);
                    break;
                case 'excel':
                    data = await this._loadExcel(source.config);
                    break;
                case 'api':
                    data = await this._loadAPI(source.config);
                    break;
                case 'database':
                    data = await this._loadDatabase(source.config);
                    break;
                case 'env':
                    data = await this._loadEnvironment(source.config);
                    break;
                default:
                    return { success: false, error: 'Unsupported source type' };
            }

            this.datasets.set(sourceId, data);
            this._indexByTags(data, sourceId);
            return { success: true, count: data.length, sourceId };
        } catch (error) {
            return { success: false, error: error.message };
        }
    }

    // Data source loaders
    async _loadJSON(config) {
        const response = await fetch(config.url || config.path);
        return await response.json();
    }

    async _loadCSV(config) {
        return this._parseCSV(config.data || '');
    }

    _parseCSV(csvText) {
        const lines = csvText.trim().split('\n');
        if (lines.length < 2) return [];
        const headers = lines[0].split(',').map(h => h.trim());
        return lines.slice(1).map(line => {
            const values = line.split(',').map(v => v.trim());
            const row = {};
            headers.forEach((h, i) => row[h] = values[i] || '');
            return row;
        });
    }

    async _loadExcel(config) {
        // Excel loading would require additional library
        console.warn('Excel loading requires SheetJS library');
        return [];
    }

    async _loadAPI(config) {
        const response = await fetch(config.endpoint, {
            method: config.method || 'GET',
            headers: config.headers || {},
            body: config.body ? JSON.stringify(config.body) : null
        });
        return await response.json();
    }

    async _loadDatabase(config) {
        // Would connect to backend endpoint
        const response = await fetch(`${API_BASE_URL}/data/load-db`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(config)
        });
        return await response.json();
    }

    async _loadEnvironment(config) {
        const envData = {};
        (config.variables || []).forEach(varName => {
            envData[varName] = window[varName] || process.env[varName] || '';
        });
        return [envData];
    }

    /**
     * 2️⃣ Dynamic Data Injection - Resolve ${data.field} placeholders
     */
    resolveDataInjection(text, dataset) {
        if (!text || typeof text !== 'string') return text;
        return text.replace(/\$\{data\.(\w+)\}/g, (match, field) => {
            return dataset[field] !== undefined ? dataset[field] : match;
        });
    }

    /**
     * Inject data into test step
     */
    injectDataIntoStep(step, dataset) {
        const injected = {};
        Object.keys(step).forEach(key => {
            if (typeof step[key] === 'string') {
                injected[key] = this.resolveDataInjection(step[key], dataset);
            } else if (typeof step[key] === 'object') {
                injected[key] = this.injectDataIntoStep(step[key], dataset);
            } else {
                injected[key] = step[key];
            }
        });
        return injected;
    }

    /**
     * 3️⃣ Smart Dataset Iteration - Multiple execution strategies
     */
    selectDatasets(sourceId, strategy = 'run-all', params = {}) {
        const datasets = this.datasets.get(sourceId) || [];
        let selected = [...datasets];

        switch (strategy) {
            case 'run-all':
                selected = datasets;
                break;
            case 'random':
                selected = this._selectRandomDatasets(datasets, params.count || 10);
                break;
            case 'first-n':
                selected = datasets.slice(0, params.count || 5);
                break;
            case 'custom-index':
                selected = (params.indices || []).map(idx => datasets[idx]).filter(d => d);
                break;
            case 'tag-based':
                selected = this._selectByTag(sourceId, params.tags || []);
                break;
            default:
                selected = datasets;
        }

        return { strategy, count: selected.length, datasets: selected };
    }

    _selectRandomDatasets(datasets, count) {
        const selected = [];
        const indices = new Set();
        while (indices.size < Math.min(count, datasets.length)) {
            indices.add(Math.floor(Math.random() * datasets.length));
        }
        indices.forEach(idx => selected.push(datasets[idx]));
        return selected;
    }

    _selectByTag(sourceId, tags) {
        const tagIndex = this.tagIndex.get(sourceId) || new Map();
        const selected = new Set();
        tags.forEach(tag => {
            (tagIndex.get(tag) || []).forEach(idx => selected.add(idx));
        });
        const datasets = this.datasets.get(sourceId) || [];
        return Array.from(selected).map(idx => datasets[idx]).filter(d => d);
    }

    _indexByTags(datasets, sourceId) {
        const tagIndex = new Map();
        datasets.forEach((data, idx) => {
            const tags = (data.tags || []).concat(data.tag ? [data.tag] : []);
            tags.forEach(tag => {
                if (!tagIndex.has(tag)) tagIndex.set(tag, []);
                tagIndex.get(tag).push(idx);
            });
        });
        this.tagIndex.set(sourceId, tagIndex);
    }

    /**
     * 4️⃣ Dataset Validation Engine - Validate before execution
     */
    registerValidationRule(fieldName, rule) {
        this.validationRules.set(fieldName, rule);
    }

    validateDataset(dataset, schema = null) {
        const result = { valid: true, errors: [], warnings: [] };
        const seen = new Set();

        Object.entries(dataset).forEach(([field, value]) => {
            const rule = this.validationRules.get(field);
            
            if (rule?.required && !value) {
                result.errors.push(`Required field missing: ${field}`);
                result.valid = false;
            }
            if (rule?.format && !rule.format.test(String(value))) {
                result.errors.push(`Invalid format for ${field}: ${value}`);
                result.valid = false;
            }
            if (rule?.unique && seen.has(value)) {
                result.warnings.push(`Duplicate value for ${field}: ${value}`);
            } else if (rule?.unique) {
                seen.add(value);
            }
        });

        // Schema validation
        if (schema) {
            Object.entries(schema).forEach(([field, fieldType]) => {
                if (typeof dataset[field] !== fieldType) {
                    result.errors.push(`Type mismatch for ${field}: expected ${fieldType}`);
                    result.valid = false;
                }
            });
        }

        return result;
    }

    validateDatasets(datasets, schema = null) {
        return datasets.map((dataset, idx) => ({
            index: idx,
            data: dataset,
            validation: this.validateDataset(dataset, schema)
        }));
    }

    /**
     * 5️⃣ Data Transformation Layer - Transform data before execution
     */
    registerTransformer(name, transformFn) {
        this.transformers.set(name, transformFn);
    }

    transformDataset(dataset, transformations = []) {
        let transformed = { ...dataset };
        transformations.forEach(({ name, params }) => {
            const transformer = this.transformers.get(name);
            if (transformer) {
                transformed = transformer(transformed, params);
            }
        });
        return transformed;
    }

    // Built-in transformers
    setupDefaultTransformers() {
        this.registerTransformer('trim', (data) => {
            const trimmed = {};
            Object.entries(data).forEach(([k, v]) => {
                trimmed[k] = typeof v === 'string' ? v.trim() : v;
            });
            return trimmed;
        });

        this.registerTransformer('lowercase', (data) => {
            const lower = {};
            Object.entries(data).forEach(([k, v]) => {
                lower[k] = typeof v === 'string' ? v.toLowerCase() : v;
            });
            return lower;
        });

        this.registerTransformer('generateToken', (data, params) => {
            data[params.field] = 'token_' + Math.random().toString(36).substr(2, 9);
            return data;
        });

        this.registerTransformer('formatCurrency', (data, params) => {
            const field = params.field;
            if (data[field]) {
                data[field] = '$' + parseFloat(data[field]).toFixed(2);
            }
            return data;
        });

        this.registerTransformer('convertDate', (data, params) => {
            const field = params.field;
            if (data[field]) {
                data[field] = new Date(data[field]).toISOString();
            }
            return data;
        });
    }

    /**
     * 6️⃣ & 7️⃣ Intelligent Data Generation - Generate test data
     */
    generateTestData(config) {
        const generated = [];
        const count = config.count || 10;

        for (let i = 0; i < count; i++) {
            const record = {};
            Object.entries(config.fields || {}).forEach(([field, type]) => {
                record[field] = this._generateValue(type, i);
            });
            generated.push(record);
        }

        this.generatedData = generated;
        return { success: true, count: generated.length, data: generated };
    }

    _generateValue(type, index) {
        switch (type) {
            case 'email':
                return `user${index + 1}@test.com`;
            case 'phone':
                return `555-${String(index + 1).padStart(3, '0')}-${String(Math.floor(Math.random() * 10000)).padStart(4, '0')}`;
            case 'name':
                const names = ['John', 'Jane', 'Bob', 'Alice', 'Charlie', 'Diana'];
                return names[index % names.length];
            case 'address':
                return `${index + 1} Main Street, City`;
            case 'creditcard':
                return `${String(Math.floor(Math.random() * 10000)).padStart(4, '0')} ${String(Math.floor(Math.random() * 10000)).padStart(4, '0')} ${String(Math.floor(Math.random() * 10000)).padStart(4, '0')} ${String(Math.floor(Math.random() * 10000)).padStart(4, '0')}`;
            case 'uuid':
                return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, c => {
                    const r = Math.random() * 16 | 0;
                    return (c === 'x' ? r : (r & 0x3 | 0x8)).toString(16);
                });
            case 'number':
                return Math.floor(Math.random() * 1000);
            case 'boolean':
                return Math.random() > 0.5;
            default:
                return `value${index + 1}`;
        }
    }

    /**
     * 8️⃣ Dataset Tagging System
     */
    tagDataset(sourceId, datasetIndex, tags) {
        const datasets = this.datasets.get(sourceId);
        if (!datasets || !datasets[datasetIndex]) return { success: false };
        
        datasets[datasetIndex].tags = tags;
        this._indexByTags(datasets, sourceId);
        return { success: true };
    }

    /**
     * 9️⃣ Dataset Execution Tracker - Track results
     */
    trackExecution(executionId, scenario, dataset, result, metadata = {}) {
        const record = {
            executionId,
            timestamp: new Date(),
            scenario,
            datasetId: dataset.id || Math.random().toString(36),
            data: this._maskSensitiveData(dataset),
            result: result, // PASS, FAIL, SKIP, RETRY
            duration: metadata.duration || 0,
            logs: metadata.logs || [],
            screenshots: metadata.screenshots || [],
            networkCalls: metadata.networkCalls || []
        };

        this.executionHistory.push(record);

        // Update analytics
        this._updateAnalytics(dataset, result);

        return { success: true, recordId: this.executionHistory.length - 1 };
    }

    _updateAnalytics(dataset, result) {
        const datasetId = dataset.id || JSON.stringify(dataset);
        
        this.analytics.executionCount++;
        if (result === 'PASS') this.analytics.successCount++;
        if (result === 'FAIL') this.analytics.failureCount++;

        if (!this.analytics.datasetMetrics.has(datasetId)) {
            this.analytics.datasetMetrics.set(datasetId, {
                executions: 0,
                passes: 0,
                failures: 0,
                retries: 0
            });
        }

        const metrics = this.analytics.datasetMetrics.get(datasetId);
        metrics.executions++;
        if (result === 'PASS') metrics.passes++;
        if (result === 'FAIL') metrics.failures++;
        if (result === 'RETRY') metrics.retries++;

        // Track failure frequency
        if (result === 'FAIL') {
            const freq = this.analytics.failureFrequency.get(datasetId) || 0;
            this.analytics.failureFrequency.set(datasetId, freq + 1);
        }
    }

    /**
     * 🔟 Flaky Dataset Detection - Identify unstable datasets
     */
    detectFlakyDatasets(failureThreshold = 0.33) {
        const flaky = [];
        this.analytics.datasetMetrics.forEach((metrics, datasetId) => {
            if (metrics.executions > 0) {
                const failureRate = metrics.failures / metrics.executions;
                if (failureRate >= failureThreshold && failureRate < 1.0) {
                    flaky.push({
                        datasetId,
                        failureRate: (failureRate * 100).toFixed(2) + '%',
                        executions: metrics.executions,
                        status: 'FLAKY'
                    });
                    this.analytics.flakyDatasets.add(datasetId);
                }
            }
        });
        return flaky.sort((a, b) => b.failureRate - a.failureRate);
    }

    /**
     * 1️⃣1️⃣ Dataset Observability - Analytics and metrics
     */
    getDatasetObservability() {
        const topFailing = [];
        this.analytics.datasetMetrics.forEach((metrics, datasetId) => {
            if (metrics.failures > 0) {
                topFailing.push({
                    datasetId,
                    failureRate: metrics.executions > 0 ? 
                        ((metrics.failures / metrics.executions) * 100).toFixed(2) + '%' : '0%',
                    totalExecutions: metrics.executions,
                    totalFailures: metrics.failures
                });
            }
        });

        const topUsed = [];
        this.analytics.datasetMetrics.forEach((metrics, datasetId) => {
            topUsed.push({
                datasetId,
                executions: metrics.executions,
                passRate: metrics.executions > 0 ?
                    ((metrics.passes / metrics.executions) * 100).toFixed(2) + '%' : '0%'
            });
        });

        return {
            summary: {
                totalExecutions: this.analytics.executionCount,
                successCount: this.analytics.successCount,
                failureCount: this.analytics.failureCount,
                successRate: this.analytics.executionCount > 0 ?
                    ((this.analytics.successCount / this.analytics.executionCount) * 100).toFixed(2) + '%' : '0%'
            },
            topFailing: topFailing.sort((a, b) => 
                parseFloat(b.failureRate) - parseFloat(a.failureRate)).slice(0, 10),
            topUsed: topUsed.sort((a, b) => 
                b.executions - a.executions).slice(0, 10),
            flakyDatasets: Array.from(this.analytics.flakyDatasets)
        };
    }

    /**
     * 1️⃣2️⃣ Dataset Coverage Engine - Analyze coverage
     */
    analyzeCoverage(scenario, coverageRules) {
        const coverage = {
            scenario,
            covered: [],
            missing: [],
            suggestedDatasets: []
        };

        Object.entries(coverageRules).forEach(([testCase, rule]) => {
            const records = this.executionHistory.filter(
                r => r.scenario === scenario && 
                r.result === rule.expectedResult
            );

            if (records.length > 0) {
                coverage.covered.push(testCase);
            } else {
                coverage.missing.push(testCase);
                coverage.suggestedDatasets.push({
                    testCase,
                    reason: `Missing scenario: ${testCase}`,
                    suggestedData: rule.suggestedData
                });
            }
        });

        return coverage;
    }

    /**
     * 1️⃣3️⃣ Data Versioning - Track dataset versions
     */
    saveDatasetVersion(datasetId, data, description = '') {
        if (!this.dataVersions.has(datasetId)) {
            this.dataVersions.set(datasetId, []);
        }

        const version = {
            version: this.dataVersions.get(datasetId).length + 1,
            timestamp: new Date(),
            data: JSON.parse(JSON.stringify(data)),
            description
        };

        this.dataVersions.get(datasetId).push(version);
        return version;
    }

    getDatasetVersions(datasetId) {
        return this.dataVersions.get(datasetId) || [];
    }

    /**
     * 1️⃣4️⃣ Dataset Diff Viewer - Compare versions
     */
    compareVersions(datasetId, version1, version2) {
        const versions = this.dataVersions.get(datasetId);
        if (!versions) return null;

        const v1 = versions[version1 - 1];
        const v2 = versions[version2 - 1];
        if (!v1 || !v2) return null;

        const diff = {
            version1: v1.version,
            version2: v2.version,
            changes: []
        };

        const allKeys = new Set([...Object.keys(v1.data), ...Object.keys(v2.data)]);
        allKeys.forEach(key => {
            if (v1.data[key] !== v2.data[key]) {
                diff.changes.push({
                    field: key,
                    oldValue: v1.data[key],
                    newValue: v2.data[key]
                });
            }
        });

        return diff;
    }

    /**
     * 1️⃣6️⃣ Dataset Retry Strategy - Intelligent retries
     */
    shouldRetryDataset(datasetId, maxRetries = 2) {
        const metrics = this.analytics.datasetMetrics.get(datasetId);
        if (!metrics) return false;
        
        // Retry if flaky and previous retries succeeded
        const isFlakyButMayPass = 
            metrics.retries < maxRetries && 
            metrics.passes > 0 && 
            metrics.failures > 0;

        return isFlakyButMayPass;
    }

    /**
     * 1️⃣7️⃣ Smart Dataset Filtering - Powerful filtering
     */
    filterDatasets(datasets, filters = {}) {
        return datasets.filter(dataset => {
            // Filter by field conditions
            if (filters.conditions) {
                return filters.conditions.every(cond => {
                    const value = dataset[cond.field];
                    switch (cond.operator) {
                        case 'equals': return value === cond.value;
                        case 'contains': return String(value).includes(cond.value);
                        case 'startsWith': return String(value).startsWith(cond.value);
                        case 'greaterThan': return Number(value) > cond.value;
                        case 'lessThan': return Number(value) < cond.value;
                        default: return true;
                    }
                });
            }

            // Filter by tags
            if (filters.tags && filters.tags.length > 0) {
                return filters.tags.some(tag => 
                    (dataset.tags || []).includes(tag) || dataset.tag === tag
                );
            }

            return true;
        });
    }

    /**
     * 1️⃣8️⃣ Data Privacy and Masking - Mask sensitive data
     */
    registerMaskingRule(field, maskPattern) {
        this.maskingRules.set(field, maskPattern);
    }

    _maskSensitiveData(data) {
        const masked = { ...data };
        this.maskingRules.forEach((pattern, field) => {
            if (masked[field]) {
                if (pattern === 'partial') {
                    masked[field] = '**** **** **** ' + String(masked[field]).slice(-4);
                } else if (pattern === 'full') {
                    masked[field] = '****';
                } else if (typeof pattern === 'function') {
                    masked[field] = pattern(masked[field]);
                }
            }
        });
        return masked;
    }

    /**
     * 1️⃣5️⃣ Parallel Dataset Execution - Support parallel runs
     */
    async executeDatasetParallel(datasets, executor, concurrency = 3) {
        const results = [];
        const executing = [];

        for (let i = 0; i < datasets.length; i++) {
            const promise = executor(datasets[i], i).then(result => {
                results[i] = result;
                return result;
            });

            executing.push(promise);

            if (executing.length >= concurrency) {
                await Promise.race(executing);
                executing.splice(executing.indexOf(promise), 1);
            }
        }

        await Promise.all(executing);
        return results;
    }

    /**
     * 1️⃣9️⃣ Dataset Failure Insights - Context when failures occur
     */
    getFailureInsights(executionId) {
        const execution = this.executionHistory.find(e => e.executionId === executionId);
        if (!execution || execution.result !== 'FAIL') return null;

        return {
            scenario: execution.scenario,
            datasetUsed: execution.data,
            duration: execution.duration,
            logs: execution.logs,
            screenshots: execution.screenshots,
            networkCalls: execution.networkCalls,
            similarFailures: this.executionHistory.filter(e =>
                e.scenario === execution.scenario &&
                e.result === 'FAIL' &&
                JSON.stringify(e.data) === JSON.stringify(execution.data)
            ).length
        };
    }

    /**
     * 2️⃣0️⃣ Execution Visualization Data - For dashboards
     */
    getVisualizationData() {
        const dataByScenario = {};
        
        this.executionHistory.forEach(record => {
            if (!dataByScenario[record.scenario]) {
                dataByScenario[record.scenario] = {
                    passed: 0,
                    failed: 0,
                    skipped: 0,
                    datasets: []
                };
            }
            
            const scenario = dataByScenario[record.scenario];
            scenario[record.result.toLowerCase()]++;
            scenario.datasets.push({
                datasetId: record.datasetId,
                result: record.result,
                timestamp: record.timestamp
            });
        });

        return dataByScenario;
    }

    /**
     * Utility: Get comprehensive engine status
     */
    getEngineStatus() {
        return {
            dataSources: this.dataSources.size,
            datasets: Array.from(this.datasets.keys()),
            datasetCounts: Object.fromEntries(
                Array.from(this.datasets.entries()).map(([k, v]) => [k, v.length])
            ),
            executionRecords: this.executionHistory.length,
            analytics: this.analytics.executionCount > 0 ? this.getDatasetObservability() : null,
            flakyDatasets: this.detectFlakyDatasets()
        };
    }
}

// Initialize global engine instance
const dataEngine = new DataDrivenEngine();
dataEngine.setupDefaultTransformers();

// Register default masking rules
dataEngine.registerMaskingRule('password', 'full');
dataEngine.registerMaskingRule('creditCard', 'partial');
dataEngine.registerMaskingRule('ssn', 'partial');
dataEngine.registerMaskingRule('apiKey', 'full');

console.log('[DataEngine] Advanced Data-Driven Testing Engine initialized');
