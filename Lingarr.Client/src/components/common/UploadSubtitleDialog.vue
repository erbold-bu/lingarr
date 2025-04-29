<template>
    <div
        v-if="isOpen"
        class="fixed inset-0 z-[1000] flex items-start justify-center bg-black bg-opacity-50 backdrop-blur-sm p-4"
        style="height: 100vh; overflow-y: auto;"
        @click="close">
        <div
            class="bg-primary border-accent my-16 max-w-md w-full rounded-lg border p-6 shadow-lg"
            @click.stop>
            <h2 class="text-primary-content mb-4 text-xl font-bold">
                {{ translate('subtitles.uploadTitle') }}
            </h2>

            <div v-if="errorMessage" class="mb-4 rounded bg-red-500 p-3 text-white">
                {{ errorMessage }}
            </div>

            <form @submit.prevent="handleUpload">
                <!-- Language Selection -->
                <div class="mb-4">
                    <label class="text-primary-content mb-2 block">
                        {{ translate('subtitles.language') }}
                    </label>
                    <select
                        v-model="selectedLanguage"
                        class="bg-primary border-accent text-primary-content w-full rounded border p-2"
                        required>
                        <option disabled value="">
                            {{ translate('subtitles.selectLanguage') }}
                        </option>
                        <option
                            v-for="lang in languages"
                            :key="lang.code"
                            :value="lang.code">
                            {{ lang.name }}
                        </option>
                    </select>
                </div>

                <!-- File Upload -->
                <div class="mb-4">
                    <label class="text-primary-content mb-2 block">
                        {{ translate('subtitles.subtitleFile') }}
                    </label>
                    <input
                        ref="fileInput"
                        type="file"
                        accept=".srt,.ssa,.ass"
                        class="bg-primary border-accent text-primary-content w-full rounded border p-2"
                        @change="handleFileSelect"
                        required />
                    <p class="text-primary-content/70 mt-1 text-sm">
                        {{ translate('subtitles.fileTypes') }}
                    </p>
                </div>

                <!-- Buttons -->
                <div class="flex justify-end space-x-2">
                    <button
                        type="button"
                        class="border-accent bg-primary text-primary-content rounded border px-4 py-2 hover:brightness-90"
                        @click="close">
                        {{ translate('common.cancel') }}
                    </button>
                    <button
                        type="submit"
                        class="bg-accent text-accent-content rounded px-4 py-2 hover:brightness-90"
                        :disabled="isUploading">
                        <span v-if="isUploading">{{ translate('subtitles.uploading') }}</span>
                        <span v-else>{{ translate('subtitles.upload') }}</span>
                    </button>
                </div>
            </form>
        </div>
    </div>
</template>

<script setup lang="ts">
import { ref, onMounted, watch } from 'vue'
import { useI18n } from '@/plugins/i18n'
import { ILanguage, IMovie, IEpisode, MediaType } from '@/ts'
import services from '@/services'
import { useSettingStore } from '@/store/setting'

const { translate } = useI18n()
const settingStore = useSettingStore()

const props = defineProps<{
    isOpen: boolean
    media: IMovie | IEpisode
    mediaType: MediaType
}>()

const emit = defineEmits(['close', 'uploaded'])

const languages = ref<ILanguage[]>([])
const selectedLanguage = ref('')
const selectedFile = ref<File | null>(null)
const errorMessage = ref('')
const isUploading = ref(false)
const fileInput = ref<HTMLInputElement | null>(null)

// Watch for dialog opening/closing
watch(() => props.isOpen, (newValue) => {
    console.log('Dialog isOpen changed to:', newValue)
    if (newValue && languages.value.length === 0) {
        // Try to load languages if dialog opens and we don't have any
        loadLanguages()
    }
})

async function loadLanguages() {
    try {
        // Get both source and target languages and combine them
        const sourceLangs = await settingStore.getSetting('source_languages')
        const targetLangs = await settingStore.getSetting('target_languages')
        
        const combinedLangs: ILanguage[] = []
        
        if (Array.isArray(sourceLangs)) {
            const sourceLanguages = sourceLangs as ILanguage[]
            combinedLangs.push(...sourceLanguages)
        }
        
        if (Array.isArray(targetLangs)) {
            const targetLanguages = targetLangs as ILanguage[]
            // Add target languages that aren't already in the list
            for (const lang of targetLanguages) {
                if (!combinedLangs.some(existing => existing.code === lang.code)) {
                    combinedLangs.push(lang)
                }
            }
        }
        
        // Sort languages alphabetically by name
        combinedLangs.sort((a, b) => a.name.localeCompare(b.name))
        
        console.log('Combined languages:', combinedLangs)
        languages.value = combinedLangs
        
        if (combinedLangs.length === 0) {
            console.error('No languages found')
            errorMessage.value = translate('errors.loadLanguages')
        }
    } catch (error) {
        console.error('Failed to load languages:', error)
        errorMessage.value = translate('errors.loadLanguages')
    }
}

onMounted(async () => {
    console.log('UploadSubtitleDialog mounted, props:', props)
    if (props.isOpen) {
        await loadLanguages()
    }
})

function handleFileSelect(event: Event) {
    const input = event.target as HTMLInputElement
    if (input.files && input.files.length > 0) {
        selectedFile.value = input.files[0]
    } else {
        selectedFile.value = null
    }
}

async function handleUpload() {
    if (!selectedFile.value || !selectedLanguage.value) {
        errorMessage.value = translate('errors.missingFields')
        return
    }

    console.log('props.media:', props.media)

    const mediaPath = `${props.media.path}/${props.media.fileName}` || ''
    if (!mediaPath) {
        errorMessage.value = translate('errors.missingMediaPath')
        return
    }

    try {
        isUploading.value = true
        errorMessage.value = ''

        await services.subtitle.uploadSubtitle(
            selectedFile.value,
            mediaPath,
            selectedLanguage.value
        )

        // Reset form
        if (fileInput.value) fileInput.value.value = ''
        selectedFile.value = null
        selectedLanguage.value = ''
        
        // Emit success event
        emit('uploaded')
        
        // Close the dialog
        close()
    } catch (error: any) {
        console.error('Upload failed:', error)
        errorMessage.value = error?.data?.message || translate('errors.uploadFailed') 
    } finally {
        isUploading.value = false
    }
}

function close() {
    emit('close')
}
</script> 