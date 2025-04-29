<template>
    <div class="bg-tertiary text-tertiary-content w-full">
        <div class="border-primary grid grid-cols-12 border-b-2 font-bold">
            <div class="col-span-1 px-4 py-2">
                <span class="hidden lg:block">
                    {{ translate('tvShows.episode') }}
                </span>
                <span class="block lg:hidden">#</span>
            </div>
            <div class="col-span-7 px-4 py-2 md:col-span-5">
                {{ translate('tvShows.episodeTitle') }}
            </div>
            <div class="col-span-4 flex justify-between py-2 pr-4 md:col-span-5">
                <span>{{ translate('tvShows.episodeSubtitles') }}</span>
                <span class="hidden md:block">
                    {{ translate('tvShows.exclude') }}
                </span>
                <span class="block md:hidden">⊘</span>
            </div>
        </div>
        <div v-for="episode in episodes" :key="episode.id" class="grid grid-cols-12">
            <div class="col-span-1 px-4 py-2">
                {{ episode.episodeNumber }}
            </div>
            <div class="col-span-7 px-4 py-2 md:col-span-5">
                {{ episode.title }}
            </div>
            <div class="col-span-4 flex justify-between pr-4 md:col-span-5">
                <div v-if="episode?.fileName" class="flex flex-wrap items-center gap-2">
                    <ContextMenu
                        v-for="(subtitle, jndex) in getSubtitle(episode.fileName)"
                        :key="`${episode.id}-${jndex}`"
                        :media-type="MEDIA_TYPE.EPISODE"
                        :media="episode"
                        :subtitle="subtitle">
                        <BadgeComponent>
                            {{ subtitle.language.toUpperCase() }}
                            <span v-if="subtitle.caption" class="text-primary-content/50">
                                - {{ subtitle.caption.toUpperCase() }}
                            </span>
                        </BadgeComponent>
                    </ContextMenu>
                    
                    <!-- Upload Button -->
                    <button 
                        class="border-accent bg-primary text-primary-content rounded-md border p-1 text-xs hover:brightness-150"
                        @click.stop.prevent="openUploadDialog(episode)">
                        <span class="px-1">+</span>
                    </button>
                </div>
                <div class="col-span-1 px-1 py-2 md:col-span-1">
                    <ToggleButton
                        v-model="episode.excludeFromTranslation"
                        size="small"
                        @toggle:update="() => showStore.exclude(MEDIA_TYPE.EPISODE, episode.id)" />
                </div>
                
            </div>
        </div>
        
        <!-- Upload Subtitle Dialog -->
        <UploadSubtitleDialog 
            v-if="selectedEpisode"
            :is-open="isUploadDialogOpen"
            :media="selectedEpisode"
            :media-type="MEDIA_TYPE.EPISODE"
            @close="isUploadDialogOpen = false"
            @uploaded="handleUploadSuccess"
        />
    </div>
</template>
<script setup lang="ts">
import { ref } from 'vue'
import { IEpisode, ISubtitle, MEDIA_TYPE } from '@/ts'
import BadgeComponent from '@/components/common/BadgeComponent.vue'
import ContextMenu from '@/components/layout/ContextMenu.vue'
import ToggleButton from '@/components/common/ToggleButton.vue'
import { useShowStore } from '@/store/show'
import UploadSubtitleDialog from '@/components/common/UploadSubtitleDialog.vue'

const props = defineProps<{
    episodes: IEpisode[]
    subtitles: ISubtitle[]
}>()
const showStore = useShowStore()

// For upload dialog
const isUploadDialogOpen = ref(false)
const selectedEpisode = ref<IEpisode | null>(null)

function openUploadDialog(episode: IEpisode) {
    console.log('Opening upload dialog for episode:', episode)
    selectedEpisode.value = episode
    setTimeout(() => {
        isUploadDialogOpen.value = true
    }, 100)
}

function handleUploadSuccess() {
    isUploadDialogOpen.value = false
    // Emit an event to refresh subtitles from parent
    emit('refresh-subtitles')
}

const getSubtitle = (fileName: string | null) => {
    if (!fileName) return null
    return props.subtitles
        .filter((subtitle: ISubtitle) => subtitle.fileName.includes(fileName))
        .slice()
        .sort((a, b) => a.language.localeCompare(b.language))
}

const emit = defineEmits(['refresh-subtitles'])
</script>
