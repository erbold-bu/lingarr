<template>
    <div class="flex flex-col space-y-2">
        <div>
            {{ translate('settings.services.geminiWarningIntro') }}
            <span :class="automationEnabled == 'true' ? 'text-red-500' : 'text-green-500'">
                {{
                    automationEnabled == 'true'
                        ? translate('settings.services.geminiEnabled')
                        : translate('settings.services.geminiDisabled')
                }}
            </span>
        </div>
        <p class="text-xs">
            {{ translate('settings.services.geminiDescription') }}
        </p>

        <label class="mb-1 block text-sm">
            {{ translate('settings.services.geminiAiModel') }}
        </label>
        <SelectComponent v-model:selected="modelSelection" :options="options" />

        <InputComponent
            v-if="isCustomModel"
            v-model="customModelId"
            validation-type="string"
            type="text"
            :label="translate('settings.services.geminiCustomModel')"
            :min-length="1"
            :error-message="translate('settings.services.geminiCustomModelError')"
            @update:validation="(val) => (customModelIsValid = val)" />

        <InputComponent
            v-model="apiKey"
            validation-type="string"
            type="password"
            :label="translate('settings.services.geminiApiKey')"
            :min-length="1"
            :error-message="translate('settings.services.geminiError')"
            @update:validation="(val) => (apiKeyIsValid = val)" />

        <AiPromptConfig @save="emit('save')" />
    </div>
</template>

<script setup lang="ts">
import { computed, ref, watch, onMounted } from 'vue'
import { useSettingStore } from '@/store/setting'
import { SETTINGS } from '@/ts'
import SelectComponent from '@/components/common/SelectComponent.vue'
import InputComponent from '@/components/common/InputComponent.vue'
import AiPromptConfig from '@/components/features/settings/services/AiPromptConfig.vue'

const CUSTOM_MODEL_VALUE = 'custom'

const options = [
    { label: 'Gemini 2.5 Pro Preview (without thinking)', value: 'gemini-2.5-pro-preview-03-25' },
    { label: 'Gemini 2.5 Flash Preview (without thinking)', value: 'gemini-2.5-flash-preview-04-17' },
    { label: 'Gemini 2.0 Flash', value: 'gemini-2.0-flash' },
    { label: 'Gemini 2.0 Flash Lite', value: 'gemini-2.0-flash-lite-preview-02-05' },
    { label: 'Gemini 1.5 Flash', value: 'gemini-1.5-flash' },
    { label: 'Gemini 1.5 Flash 8B', value: 'gemini-1.5-flash-8b' },
    { label: 'Gemini 1.5 Pro', value: 'gemini-1.5-pro' },
    { label: 'Custom Model ID', value: CUSTOM_MODEL_VALUE }
]
const settingsStore = useSettingStore()
const emit = defineEmits(['save'])
const apiKeyIsValid = ref(false)
const customModelIsValid = ref(false)
const customModelId = ref('')
const modelSelection = ref('')

const automationEnabled = computed(() => settingsStore.getSetting(SETTINGS.AUTOMATION_ENABLED))
const isCustomModel = computed(() => modelSelection.value === CUSTOM_MODEL_VALUE)

onMounted(() => {
    initializeModelValues()
})

function initializeModelValues() {
    const savedModel = settingsStore.getSetting(SETTINGS.GEMINI_MODEL) as string
    
    // Check if the saved model is in the predefined options
    const isStandardModel = options.some(opt => opt.value === savedModel)
    
    if (isStandardModel) {
        modelSelection.value = savedModel
    } else {
        // If it's a custom model
        modelSelection.value = CUSTOM_MODEL_VALUE
        if (savedModel) {
            customModelId.value = savedModel
            customModelIsValid.value = true
        }
    }
}

// Watch for changes to the model selection
watch(modelSelection, (newValue) => {
    if (newValue !== CUSTOM_MODEL_VALUE) {
        // For predefined models, directly update the setting
        settingsStore.updateSetting(SETTINGS.GEMINI_MODEL, newValue, true)
        emit('save')
    }
})

// Watch for changes to customModelId and update settings accordingly
watch(customModelId, (newValue) => {
    if (isCustomModel.value && customModelIsValid.value) {
        settingsStore.updateSetting(SETTINGS.GEMINI_MODEL, newValue, true)
        emit('save')
    }
})

const apiKey = computed({
    get: () => settingsStore.getSetting(SETTINGS.GEMINI_API_KEY) as string,
    set: (newValue: string) => {
        settingsStore.updateSetting(SETTINGS.GEMINI_API_KEY, newValue, apiKeyIsValid.value)
        if (apiKeyIsValid.value) {
            emit('save')
        }
    }
})
</script>
