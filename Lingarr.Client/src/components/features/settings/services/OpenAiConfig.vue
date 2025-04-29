<template>
    <div class="flex flex-col space-y-2">
        <div>
            {{ translate('settings.services.openAiWarningIntro') }}
            <span :class="automationEnabled == 'true' ? 'text-red-500' : 'text-green-500'">
                {{
                    automationEnabled == 'true'
                        ? translate('settings.services.openAiEnabled')
                        : translate('settings.services.openAiDisabled')
                }}
            </span>
        </div>
        <p class="text-xs">
            {{ translate('settings.services.openAiDescription') }}
        </p>

        <label class="mb-1 block text-sm">
            {{ translate('settings.services.openAiAiModel') }}: {{ modelSelection }}
        </label>
        <SelectComponent v-model:selected="modelSelection" :options="options" />

        <InputComponent
            v-if="isCustomModel"
            v-model="customModelId"
            validation-type="string"
            type="text"
            :label="translate('settings.services.openAiCustomModel')"
            :min-length="1"
            :error-message="translate('settings.services.openAiCustomModelError')"
            @update:validation="(val) => (customModelIsValid = val)" />

        <InputComponent
            v-model="apiKey"
            validation-type="string"
            type="password"
            :label="translate('settings.services.openAiApiKey')"
            :min-length="1"
            :error-message="translate('settings.services.openAiError')"
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
    { label: 'O3', value: 'o3' },
    { label: 'O4-mini', value: 'o4-mini' },
    { label: 'GPT-4.1', value: 'gpt-4.1' },
    { label: 'GPT-4.1 Mini', value: 'gpt-4.1-mini' }, 
    { label: 'GPT-4.1 Nano', value: 'gpt-4.1-nano' },
    { label: 'GPT-4o', value: 'gpt-4o' },
    { label: 'GPT-4o Mini', value: 'gpt-4o-mini' },
    { label: 'GPT-4 Turbo', value: 'gpt-4-turbo' },
    { label: 'GPT-4', value: 'gpt-4' },
    { label: 'GPT-3.5 Turbo', value: 'gpt-3.5-turbo' },
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
    const savedModel = settingsStore.getSetting(SETTINGS.OPENAI_MODEL) as string
    
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
        settingsStore.updateSetting(SETTINGS.OPENAI_MODEL, newValue, true)
        emit('save')
    }
})

// Watch for changes to customModelId and update settings accordingly
watch(customModelId, (newValue) => {
    if (isCustomModel.value && customModelIsValid.value) {
        settingsStore.updateSetting(SETTINGS.OPENAI_MODEL, newValue, true)
        emit('save')
    }
})

const apiKey = computed({
    get: () => settingsStore.getSetting(SETTINGS.OPENAI_API_KEY) as string,
    set: (newValue: string) => {
        settingsStore.updateSetting(SETTINGS.OPENAI_API_KEY, newValue, apiKeyIsValid.value)
        if (apiKeyIsValid.value) {
            emit('save')
        }
    }
})
</script>
