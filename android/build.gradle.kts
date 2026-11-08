// Khai bao phien ban plugin dung chung cho ca project (apply false = chua ap dung o day).
plugins {
    id("com.android.application") version "8.5.2" apply false
    id("org.jetbrains.kotlin.android") version "1.9.24" apply false
    // Sinh code doc/ghi JSON cho cac class @Serializable.
    id("org.jetbrains.kotlin.plugin.serialization") version "1.9.24" apply false
}
