<template>
    <div class="relative items-center transition duration-300 ease-in-out select-none">
        <!-- Context -->
        <TooltipComponent ref="tooltip" alignment="left">
            <div ref="clickOutside" @click="toggle">
                <slot></slot>
            </div>
        </TooltipComponent>
        <!-- Menu -->
        <div
            v-show="isOpen"
            ref="excludeClickOutside"
            class="border-accent bg-primary absolute top-8 right-0 z-10 w-56 rounded-md border bg-clip-border shadow-lg">
            <div class="px-3 py-1" role="menu" aria-orientation="vertical">
                <span class="text-xs" role="menuitem">Translate to ...</span>
                <div
                    v-for="language in languages"
                    :key="language.code"
                    class="mb-1 flex text-sm"
                    role="menuitem"
                    @click="selectOption(language)">
                    <span class="h-full w-full cursor-pointer py-2 hover:brightness-150">
                        {{ language.name }}
                    </span>
                </div>
                <div class="border-accent my-2 border-t"></div>
                <div
                    class="mb-1 flex text-sm"
                    role="menuitem"
                    @click="downloadSubtitle">
                    <span class="h-full w-full cursor-pointer py-2 hover:brightness-150">
                        Download
                    </span>
                </div>
            </div>
        </div>
    </div>
    
    <!-- Upload Subtitle Dialog -->
    <UploadSubtitleDialog 
        :is-open="isUploadDialogOpen"
        :media="media"
        :media-type="mediaType"
        @close="isUploadDialogOpen = false"
        @uploaded="handleUploadSuccess"
    />
</template>

<script setup lang="ts">
import { ref, Ref, computed, ComputedRef } from 'vue'
import { IEpisode, ILanguage, IMovie, ISubtitle, MediaType } from '@/ts'
import { useSettingStore } from '@/store/setting'
import { useTranslateStore } from '@/store/translate'
import useClickOutside from '@/composables/useClickOutside'
import TooltipComponent from '@/components/common/TooltipComponent.vue'
import services from '@/services'
import UploadSubtitleDialog from '@/components/common/UploadSubtitleDialog.vue'

const emit = defineEmits(['update:toggle'])
const { media, subtitle, mediaType } = defineProps<{
    media: IMovie | IEpisode
    subtitle: ISubtitle
    mediaType: MediaType
}>()
const settingsStore = useSettingStore()
const translateStore = useTranslateStore()

const tooltip = ref<InstanceType<typeof TooltipComponent> | null>(null)
const isOpen: Ref<boolean> = ref(false)
const clickOutside: Ref = ref(null)
const excludeClickOutside: Ref = ref(null)
const isUploadDialogOpen = ref(false)

const languages: ComputedRef<ILanguage[]> = computed(
    () => settingsStore.getSetting('target_languages') as ILanguage[]
)

function toggle() {
    emit('update:toggle')
    isOpen.value = !isOpen.value
}

function selectOption(target: ILanguage) {
    translateStore.translateSubtitle(media.id, subtitle, subtitle.language, target, mediaType)
    toggle()
    tooltip.value?.showTooltip()
}

function downloadSubtitle() {
    // Use the subtitle service to download the file with proper authentication
    services.subtitle.downloadSubtitle(subtitle.path)
        .then(blob => {
            // Create a URL for the blob
            const url = window.URL.createObjectURL(blob)
            
            // Create an anchor element to trigger download
            const a = document.createElement('a')
            a.href = url
            
            // Extract the filename from the path, which should include the extension
            const pathParts = subtitle.path.split('/')
            const fullFileName = pathParts[pathParts.length - 1]
            
            a.download = fullFileName
            document.body.appendChild(a)
            a.click()
            
            // Clean up
            window.URL.revokeObjectURL(url)
            document.body.removeChild(a)
            
            // Close the menu
            toggle()
            tooltip.value?.showTooltip()
        })
        .catch(error => {
            console.error('Error downloading subtitle:', error)
            // Could add error handling/notification here
        })
}


function handleUploadSuccess() {
    // Refresh the subtitle list
    emit('update:toggle')
    tooltip.value?.showTooltip()
}

useClickOutside(
    clickOutside,
    () => {
        isOpen.value = false
    },
    excludeClickOutside
)
</script>
